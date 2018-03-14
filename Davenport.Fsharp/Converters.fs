module Davenport.Converters

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Types

// The Fable JsonConverter uses a cache, so it's best to just instantiate it once.
let fableConverter = Fable.JsonConverter()
let defaultSerializerSettings = JsonSerializerSettings()
defaultSerializerSettings.Converters.Add fableConverter
defaultSerializerSettings.NullValueHandling <- NullValueHandling.Ignore

let makeJsonWriter () = 
    let stream = new System.IO.MemoryStream()
    let streamWriter = new System.IO.StreamWriter(stream) :> System.IO.TextWriter
    new JsonTextWriter(streamWriter)

type JsonKey = string

type JsonValue = 
    | StringProp of JsonKey * string 
    | IntProp of JsonKey * int 
    | ObjectProp of JsonKey * JsonValue
    | ArrayProp of JsonKey * JsonValue list
    | RawProp of JsonKey * string
    | String of string 
    | Int of int 
    | Object of JsonValue 
    | Array of JsonValue list
    | Raw of string

type JsonObjectBuilder() = 
    member __.Yield(x: JsonValue) = [x]
    member __.Zero() = [] // Empty list
    member __.Delay(x) = x()
    member __.Combine(a: JsonValue list, b: JsonValue list): JsonValue list = a@b
    member __.Run(values: JsonValue list) =     
        use stream = new System.IO.MemoryStream()
        use streamWriter = new System.IO.StreamWriter(stream) :> System.IO.TextWriter
        use writer = new JsonTextWriter(streamWriter)
        writer.WriteStartObject()

        // TODO: Iterate through the list and build an object

        writer.WriteEndObject()        
        use reader = new System.IO.StreamReader(stream)
        reader.ReadToEnd()

let jsonObject = JsonObjectBuilder()

let asdf = jsonObject {
    yield StringProp ("hello", "world")
    yield IntProp ("foo", 5)
}

let writeInsertedDocument (fieldMappings: FieldMapping) (writer: JsonWriter) (doc: InsertedDocument) (serializer: JsonSerializer) = 
    writer.WriteStartObject()

    let (typeName, docValue) = doc
    let docValueType = doc.GetType()
    let j = JObject.FromObject(docValue, serializer)

    typeName
    |> Option.iter (fun typeName ->
        writer.WritePropertyName "type"
        writer.WriteValue typeName
    )

    let (idField, revField) =
        match typeName with 
        | None -> None
        | Some typeName -> Map.tryFind typeName fieldMappings
        |> Option.defaultValue ("_id", "_rev")

    [
        j.[idField], idField, "_id", "Id";
        j.[revField], revField, "_rev", "Rev";
    ]
    |> Seq.iter (fun (token, givenFieldName, canonFieldName, readableFieldName) ->
        match isNull token with 
        | true -> 
            sprintf "%s field '%s' was not found on type %s. If you want to map it to a custom field on your type, use Davenport's `converterSettings` function to pass a list of field mappings." readableFieldName givenFieldName docValueType.FullName
            |> System.ArgumentException
            |> raise
        | false ->
            let value = j.[idField].Value<string>()

            if System.String.IsNullOrEmpty value |> not
            then 
                writer.WritePropertyName canonFieldName
                writer.WriteValue value
    )

    Seq.cast<JProperty> j 
    |> Seq.filter (fun prop -> prop.Name <> "type" && prop.Name <> idField && prop.Name <> revField)
    |> Seq.iter (fun prop -> prop.WriteTo(writer))       

    writer.WriteEndObject()

let private encodeOption o = JsonConvert.SerializeObject(o, defaultSerializerSettings)

let convertRevToMap rev = Map.ofSeq ["rev", rev]

let convertListOptionsToMap (options: ListOption list) = 
    let rec inner remaining qs = 
        match remaining with 
        | ListLimit l::rest -> 
            Map.add "limit" (string l) qs
            |> inner rest
        | Key k::rest ->
            Map.add "key" (encodeOption k) qs
            |> inner rest
        | Keys k::rest ->
            Map.add "keys" (encodeOption k) qs
            |> inner rest
        | StartKey k::rest ->
            Map.add "start_key" (encodeOption k) qs
            |> inner rest
        | EndKey k::rest ->
            Map.add "end_key" (encodeOption k) qs
            |> inner rest
        | InclusiveEnd i::rest ->
            Map.add "inclusive_end" (string i) qs
            |> inner rest
        | Direction d::rest ->
            let value = 
                match d with 
                | Descending -> true
                | Ascending -> false

            Map.add "descending" (string value) qs
            |> inner rest
        | ListOption.Skip s::rest ->
            Map.add "skip" (string s) qs
            |> inner rest
        | Reduce r::rest ->
            Map.add "reduce" (string r) qs
            |> inner rest
        | Group g::rest ->
            Map.add "group" (string g) qs
            |> inner rest
        | GroupLevel l::rest ->
            Map.add "group_level" (string l) qs
            |> inner rest
        | [] -> qs

    inner options Map.empty

let convertSortListToMap (sorts: Sort list) = 
    // When serialized, we want the json to look like: [{"fieldName1": "asc"}, {"fieldName2": "desc"}]
    let rec inner sorts output = 
        match sorts with 
        | [] -> output 
        | Sort (field, dir)::rest -> 
            let item = 
                match dir with
                | Ascending -> "asc"
                | Descending -> "desc"
                |> fun d -> [field, d]
                |> Map.ofSeq

            inner rest (output@[item])

    inner sorts []

let convertFindOptionsToMap (options: FindOption list) = 
    let rec inner remaining qs = 
        match remaining with 
        | Fields f::rest ->
            Map.add "fields" (encodeOption f) qs
            |> inner rest
        | SortBy s::rest ->
            Map.add "sort" (convertSortListToMap s |> encodeOption) qs
            |> inner rest
        | FindLimit l::rest ->
            Map.add "limit" (string l) qs
            |> inner rest
        | FindOption.Skip s::rest ->
            Map.add "skip" (encodeOption s) qs
            |> inner rest
        | UseIndex i::rest ->
            Map.add "use_index" (encodeOption i) qs
            |> inner rest
        | Selector s::rest ->
            Map.add "selector" (encodeOption s) qs
            |> inner rest
        | [] -> qs
    
    inner options Map.empty


// 2018-03-13
// Another problem: our alias types such as InsertedDocument aren't passed to the WriteJson function. Instead
// they come through as the underlying (string option * 'a) type. 
//
// First solution coming to mind is to just dump the json converter and add type-specific functions for reading
// and writing.

// 2018-03-12
// Current problem: It's now set up to easily pass the Id and Rev field names when writing to JSON, but
// this converter currently has no way to know what to parse them back to. That is to say, it knows what 
// they're named when writing json, but it doesn't know what they're named when reading json.
// 
// Easiest solution to this, I think, is having some sane defaults. We can read the _id and _rev properties
// and also add them to Id and Rev, id and rev, ID and REV, _ID and _REV as long as they don't exist. When 
// deserialized the JsonConverter will just discard the unnecessary ones.
//
// We could then extend those defaults with a field on the couchProps that ofJson and toJson already get.
// Those two functions will have to somehow assemble/disassemble those fields. Maybe some kind of Union
// type, CustomFields of (fieldName: string list) * (json: string).

// 2018-03-10
// Thinking it would be really easy to handle the typenames by making the Davenport methods
// require a union type of InsertedDoc (typename: string * data: obj). We don't need to care about
// the deserialized data type because the dev will be able determine that using the type string.
// It could even use a union type for the data object, letting users pass a raw json string or an
// object that will be stringified by the converter.

// 2018-03-06 16:50 
// Current intended usage:
// singleDocType typeof<RandomType> "random-type"
// |> ...configure other client stuff
// |> get id rev
// |> fun (typeName, jtoken, defaultConverter) -> if typeName == "random-type" then jtoken.ToObject<RandomType>()
// for custom deserialization
// OR
// |> deserialize<RandomType> for default deserialization

// 2018-03-05 
// Trying to figure out how to get from this point, where we know the string type that was written by x.WriteJson,
// to converting the result to a union type.
// Maybe add a `multipleDocTypes` function to the library itself, and that function accepts a list of the TypeString * System.Type * Id field * Rev field
// to map all the types it will deal with. Then the original FsConverter receives those types (if it's not in multiple doc mode the converter still receives the
// list, just with one single element.)

type DefaultConverter (_fieldMappings: FieldMapping) = 
    inherit ICouchConverter()

    let mutable fieldMappings: FieldMapping = _fieldMappings

    override __.AddFieldMappings mapping = 
        // Merge the new mapping into the old one, overwriting old keys if necessary.
        fieldMappings <- Map.fold (fun state key value -> Map.add key value state) fieldMappings mapping

    override __.GetFieldMappings() = fieldMappings

    override __.ConvertListOptionsToMap options = convertListOptionsToMap options

    override __.ConvertFindOptionsToMap options = convertFindOptionsToMap options 

    override __.ConvertRevToMap rev = convertRevToMap rev

    override __.WriteInsertedDocument mapping writer doc = writeInsertedDocument mapping writer doc

    override x.WriteJson(writer: JsonWriter, objValue: obj, serializer: JsonSerializer) =
        match objValue with 
        | :? Serializable as inserted -> 
            x.WriteJson(t, obj, serializer)
            writeInsertedDocument (x.GetFieldMappings()) writer inserted serializer
        | _ -> failwithf "FsConverter.WriteJson: Unsupported object type %A." (objValue.GetType())