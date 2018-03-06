module Converter

open Newtonsoft.Json
open Newtonsoft.Json.Linq

/// The Fable JsonConverter uses a cache, so it's best to just instantiate it once.
let private fableConverter = Fable.JsonConverter()

// We don't want to tie the type converter to a single generic type as it will ideally be parsing multiple
// different types (different types stored in the same database and queried with views relying on those types).
// Off the top of my head I'm envisioning giving this converter a list of [typeString, type, typeString -> type] tuple. 
// The big question is how to convert a result (e.g. list of those types given above) into a union type which can be returned.
// Maybe a second list of [UnionType, obj -> UnionType] tuples? The first list of tuples converts the individual docs, the second
// list of tuples transforms the docs into union types? Return an option to skip that transformation?

type SupportedTypeName = string

type SupportedType = SupportedTypeName * System.Type

type ObjectType = 
    | SystemType of System.Type 
    | StringType of string
    | JtokenOptionType of JToken option

type FsTypeConverter(idField: string, revField: string, supportedTypes: SupportedType list) = 
    inherit JsonConverter()

    member __.CustomConverter = fableConverter :> JsonConverter

    member private x.CanConvertDirectly = function  
        | SystemType objectType -> 
            supportedTypes
            |> Seq.exists (fun (_, t) -> t = objectType)
        | StringType objectType ->
            supportedTypes 
            |> Seq.exists (fun (t, _) -> t = objectType)
        | JtokenOptionType (Some s) -> 
            s.Value<string>()
            |> StringType
            |> x.CanConvertDirectly 
        | JtokenOptionType None -> false 

    override x.CanConvert objectType = 
        x.CanConvertDirectly (SystemType objectType) || x.CustomConverter.CanConvert objectType

    override x.ReadJson(reader: JsonReader, objectType: System.Type, existingValue: obj, serializer: JsonSerializer) = 
        let j = JObject.Load reader 
        let docTypeToken: JToken option = Option.ofObj j.["type"]

        if JtokenOptionType docTypeToken |> x.CanConvertDirectly |> not 
        then 
            x.CustomConverter.ReadJson(reader, objectType, existingValue, serializer)
        else 

        let docType = 
            docTypeToken 
            |> Option.get 
            |> fun t -> t.Value<string>()
        let id: JToken option = Option.ofObj j.["_id"]
        let rev: JToken option = Option.ofObj j.["_rev"]

        // Rename the _id and _rev fields to whatever the type expects them to be
        match id, idField = "_id" with 
        | None, _
        | Some _, true -> ()
        | Some value, false -> 
            j.Remove("_id") |> ignore 
            j.Add(idField, value)

        match rev, revField = "_rev" with 
        | None, _
        | Some _, true -> ()
        | Some value, false ->
            j.Remove("_rev") |> ignore
            j.Add(revField, value)

        // Trying to figure out how to get from this point, where we know the string type that was written by x.WriteJson,
        // to converting the result to a union type.

        // Maybe add a `multipleDocTypes` function to the library itself, and that function accepts a list of the TypeString * System.Type * Id field * Rev field
        // to map all the types it will deal with. Then the original FsConverter receives those types (if it's not in multiple doc mode the converter still receives the
        // list, just with one single element.)

        Map.empty :> obj

    override x.WriteJson(writer: JsonWriter, objValue: obj, serializer: JsonSerializer) =
        let objectType = objValue.GetType()

        if SystemType objectType |> x.CanConvertDirectly |> not
        then
            x.CustomConverter.WriteJson(writer, objValue, serializer)
        else

        writer.WriteStartObject()

        let typeName, _ = 
            supportedTypes 
            |> Seq.find (fun (_, t) -> t = objectType)

        // Write the type directly to the document so we can use it when reading json to determine which type it should be parsed to.
        writer.WritePropertyName "type"
        writer.WriteValue typeName

        // Convert the object to a JObject so we can pluck out fields
        let j = JObject.FromObject objValue

        // Find the data object's id and rev fields.
        // A JObject field will be null if it doesn't exist, but will return a JToken with Null value if the field does exist and it's null.
        let id =
            if isNull j.[idField]
            then sprintf "Id field '%s' was not found on type %s." idField objectType.FullName |> System.ArgumentException |> raise
            else j.[idField]

        let rev =
            if isNull j.[revField]
            then sprintf "Rev field '%s' was not found on type %s." revField objectType.FullName |> System.ArgumentException |> raise
            else j.[revField]

        // Write the _id and _rev values if they aren't null or empty. Writing either one when it isn't intended can make CouchDB throw an error.
        [id, "_id"; rev, "_rev"]
        |> Seq.iter (fun (token, name) ->
            let value = token.Value<string>()

            if System.String.IsNullOrEmpty value |> not then
                writer.WritePropertyName name
                writer.WriteValue value
        )

        // Write everything else
        Seq.cast<JProperty> j
        |> Seq.filter (fun prop -> prop.Name <> "type" && prop.Name <> idField && prop.Name <> revField)
        |> Seq.iter (fun prop -> prop.WriteTo(writer, [|x.CustomConverter|]))

        writer.WriteEndObject()