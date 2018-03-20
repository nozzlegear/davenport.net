module Davenport.Converters

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Types
open Infrastructure

// The Fable JsonConverter uses a cache, so it's best to just instantiate it once.
// let fableConverter = Fable.JsonConverter()

// let defaultSerializerSettings = JsonSerializerSettings()
// defaultSerializerSettings.NullValueHandling <- NullValueHandling.Ignore
// defaultSerializerSettings.Converters.Add fableConverter
let defaultSerializerSettings = Microsoft.FSharpLu.Json.Compact.Internal.Settings.settings
defaultSerializerSettings.NullValueHandling <- NullValueHandling.Ignore
defaultSerializerSettings.MissingMemberHandling <- MissingMemberHandling.Ignore
    // Microsoft.FSharpLu.Json.Default.Internal.DefaultSettings.settings

let defaultSerializer = JsonSerializer.Create(defaultSerializerSettings)

type JsonObjectBuilder() = 
    member __.Yield(x: JsonValue) = [x]
    member __.Yield(x: JsonValue list) = x
    member __.YieldFrom(x: JsonValue option) = 
        x
        |> Option.map (fun x -> [x])
        |> Option.defaultValue []
    member __.YieldFrom(x: JsonValue option list) =
        x
        |> List.filter Option.isSome
        |> List.map Option.get
    member __.Zero() = [] // Empty list
    member __.Delay(x) = x()
    member __.Combine(a: JsonValue list, b: JsonValue list): JsonValue list = a@b
    member __.Run(values: JsonValue list) =     
        // Json text writer example: 
        // https://www.newtonsoft.com/json/help/html/WriteJsonWithJsonTextWriter.htm

        let sb = new System.Text.StringBuilder()
        let sw = new System.IO.StringWriter(sb)
        use writer = new JsonTextWriter(sw)

        writer.WriteStartObject()

        // Iterate through the list and build an object
        let rec inner remaining = 
            match remaining with 
            | [] -> ()
            | (String x)::rest -> 
                writer.WriteValue x
                inner rest
            | (Int x)::rest ->
                writer.WriteValue x
                inner rest
            | (Bool x)::rest -> 
                writer.WriteValue x 
                inner rest
            | (Object x)::rest ->
                writer.WriteStartObject()
                inner x
                writer.WriteEndObject()
                inner rest
            | (Array x)::rest ->
                writer.WriteStartArray()
                inner x
                writer.WriteEndArray()
                inner rest
            | (Raw x)::rest ->
                writer.WriteRawValue x
                inner rest 
            | StringProp (k, x)::rest ->
                writer.WritePropertyName k
                writer.WriteValue x
                inner rest 
            | IntProp (k, x)::rest ->
                writer.WritePropertyName k
                writer.WriteValue x 
                inner rest 
            | BoolProp (k, x)::rest ->
                writer.WritePropertyName k
                writer.WriteValue x
                inner rest
            | ObjectProp (k, x)::rest ->
                writer.WritePropertyName k 
                writer.WriteStartObject()
                inner x
                writer.WriteEndObject()
                inner rest 
            | ArrayProp (k, x)::rest ->
                writer.WritePropertyName k
                writer.WriteStartArray()
                inner x 
                writer.WriteEndArray()
                inner rest
            | RawProp (k, x)::rest ->
                writer.WritePropertyName k 
                writer.WriteRawValue x 
                inner rest
            | JProp x::rest ->
                x.WriteTo writer 
                inner rest

        inner values

        writer.WriteEndObject()   
        sb.ToString()

let jsonObject = JsonObjectBuilder()

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

type DefaultConverter () = 
    inherit ICouchConverter()

    let stringify o = JsonConvert.SerializeObject(o, defaultSerializerSettings)

    let convertSortListToJsonValues (sorts: Sort list) = 
        // When serialized, we want the json to look like: [{"fieldName1": "asc"}, {"fieldName2": "desc"}]
        let rec inner sorts output = 
            match sorts with 
            | [] -> output 
            | Sort (field, dir)::rest -> 
                let item = 
                    match dir with
                    | Ascending -> "asc"
                    | Descending -> "desc"
                    |> fun dir -> [StringProp(field, dir)]
                    |> JsonValue.Object

                inner rest (output@[item])

        inner sorts []

    override __.ConvertListOptionsToMap options = 
        let rec inner remaining qs = 
            match remaining with 
            | ListLimit l::rest -> 
                Map.add "limit" (string l) qs
                |> inner rest
            | Key k::rest ->
                Map.add "key" (stringify k) qs
                |> inner rest
            | Keys k::rest ->
                Map.add "keys" (stringify k) qs
                |> inner rest
            | StartKey k::rest ->
                Map.add "start_key" (stringify k) qs
                |> inner rest
            | EndKey k::rest ->
                Map.add "end_key" (stringify k) qs
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

    override __.ConvertRevToMap rev = Map.ofSeq ["rev", rev]

    override __.WriteInsertedDocument (fieldMappings: FieldMapping) (doc: InsertedDocument<_>) = jsonObject { 
        let (typeName, docValue) = doc
        let docValueType = doc.GetType()
        let j = JObject.FromObject(docValue, defaultSerializer)

        yield!
            typeName
            |> Option.map (fun t -> StringProp ("type", t))

        let (idField, revField) =
            match typeName with 
            | None -> None
            | Some typeName -> Map.tryFind typeName fieldMappings
            |> Option.defaultValue ("_id", "_rev")

        yield!
            [
                j.[idField], idField, "_id", "Id";
                j.[revField], revField, "_rev", "Rev";
            ]
            |> List.map (fun (token, givenFieldName, canonFieldName, readableFieldName) ->
                match isNull token with 
                | true -> 
                    sprintf "%s field '%s' was not found on type %s. If you want to map it to a custom field on your type, use Davenport's `converterSettings` function to pass a list of field mappings." readableFieldName givenFieldName docValueType.FullName
                    |> System.ArgumentException
                    |> raise
                | false ->
                    let value = j.[givenFieldName].Value<string>()

                    match System.String.IsNullOrEmpty value with 
                    | true -> None 
                    | false ->
                        Some (StringProp (canonFieldName, value))
            )

        yield
            Seq.cast<JProperty> j 
            |> Seq.filter (fun prop -> prop.Name <> "type" && prop.Name <> idField && prop.Name <> revField)
            |> Seq.map JProp
            |> List.ofSeq
    }

    override x.WriteBulkInsertList mapping mode docs = jsonObject {
        // Desired json looks like { "new_edits": true, "docs": [{doc1}, {doc2}, {doc3}] }

        yield
            match mode with 
            | AllowNewEdits -> true
            | NoNewEdits -> false
            |> fun b -> BoolProp("new_edits", b)

        yield
            docs 
            |> List.map (x.WriteInsertedDocument mapping >> JsonValue.Raw)
            |> fun l -> ArrayProp("docs", l)
    }

    override __.WriteDesignDoc views = jsonObject {
        // Desired json looks like { "language": "javascript", "views": { "view1": { "map": "...", "reduce": "..." } } }

        let viewData = 
            views
            |> Seq.map (fun kvp -> 
                let (map, reduce) = kvp.Value

                match reduce with 
                | None -> None
                | Some reduce -> Some <| StringProp("reduce", reduce)
                |> List.ofSingle
                |> List.appendSingle (Some <| StringProp("map", map))
                |> List.filter Option.isSome 
                |> List.map Option.get
                |> fun v -> ObjectProp(kvp.Key, v))
            |> List.ofSeq

        yield ObjectProp("views", viewData)
        // JS is the only supported language by CouchDB
        yield StringProp("language", "javascript")
    }

    override __.WriteIndexes name fields = jsonObject {
        // Desired json looks like { "name" : "index-name", "fields": ["field1", "field2", "field3"]}
        yield StringProp ("name", name)
        yield ArrayProp ("fields", fields |> List.map JsonValue.String)
    }

    override __.WriteFindSelector options selector = jsonObject {
        // Desired json looks like {"fields": ["_id"], "sort": [{"fieldName": "asc", "fieldName2": "desc"}], "selector": {"fieldName": {"$eq", "some value"}}}
        let rec getOptions remaining out = 
            match remaining with 
            | Fields f::rest ->
                getOptions rest (out@[ArrayProp("fields", f |> List.map JsonValue.String)])
            | SortBy s::rest ->
                getOptions rest (out@[ArrayProp("sort", convertSortListToJsonValues s)])
            | FindLimit l::rest ->
                getOptions rest (out@[IntProp("limit", l)])
            | FindOption.Skip s::rest ->
                getOptions rest (out@[IntProp("skip", s)])
            | UseIndex i::rest ->
                getOptions rest (out@[RawProp("use_index", stringify i)])
            | [] -> out

        let rec getSelector remaining out =
            match remaining with
            | EqualTo x::rest ->
                getSelector rest (out@[RawProp("$eq", stringify x)])
            | NotEqualTo x::rest ->
                getSelector rest (out@[RawProp("$ne", stringify x)])
            | GreaterThan x::rest ->
                getSelector rest (out@[RawProp("$gt", stringify x)])
            | LesserThan x::rest ->
                getSelector rest (out@[RawProp("$lt", stringify x)])
            | GreaterThanOrEqualTo x::rest ->
                getSelector rest (out@[RawProp("$gte", stringify x)])
            | LessThanOrEqualTo x::rest ->
                getSelector rest (out@[RawProp("$lte", stringify x)])
            | [] -> out

        let selectorProps = 
            selector 
            |> Seq.map (fun kvp -> ObjectProp(kvp.Key, getSelector kvp.Value []))
            |> List.ofSeq

        yield getOptions options []
        yield ObjectProp("selector", selectorProps)
    }

    override __.ReadAsJToken mapping json =
        let token = 
            match json with 
            | JsonString s -> JsonConvert.DeserializeObject<JToken>(s, defaultSerializerSettings)
            | JsonToken t -> t
            | JsonObject o -> o :> JToken

        match token.Type with 
        | JTokenType.Object ->
            let token = JObject.FromObject(token, defaultSerializer)
            let typeName = 
                token.["type"]
                |> Option.ofObj 
                |> Option.filter (fun t -> t.Type = JTokenType.String)
                |> Option.map (fun t -> t.Value<string>())

            typeName
            |> Option.bind (fun t -> mapping |> Map.tryFind t)
            |> Option.iter (fun (idField, revField) -> 
                [
                    Option.ofObj token.["_id"], idField
                    Option.ofObj token.["_rev"], revField
                ]
                |> Seq.filter (fun (field, _) -> Option.isSome field)
                |> Seq.map (fun (field, x) -> Option.get field, x)
                |> Seq.iter (fun (field, newFieldName) -> token.Add(newFieldName, field)))

            typeName, token :> JToken

        | _ -> None, token 

    override x.ReadAsDocument mapping json = 
        let (typeName, j) = x.ReadAsJToken mapping (JsonString json)

        Document(typeName, j, defaultSerializer)

    override x.ReadAsViewResult mapping json = 
        let (_, j) = x.ReadAsJToken mapping (JsonString json) 
        let offset = j.Value<int>("offset")
        let totalRows = j.Value<int>("total_rows")
        
        let rec keyValue (d: JToken): KeyValue = 
            match d.Type with 
            | JTokenType.Null -> 
                KeyValue.Null
            | JTokenType.String -> 
                d.Value<string>()
                |> KeyValue.String 
            | JTokenType.Integer ->
                let str = d.Value<string>()

                // First try to parse the value as an Int. If it's too long it will return None and we'll parse as an int64 instead.
                // If both parses fail, fall back to returning the value as a string.
                str 
                |> Int.parse
                |> Option.map KeyValue.Int
                |> Option.defaultBindWith(fun _ ->
                    str 
                    |> Long.parse 
                    |> Option.map KeyValue.Long)
                |> Option.defaultValue (KeyValue.String str)
            | JTokenType.Float ->
                d.Value<float>()
                |> KeyValue.Float 
            | JTokenType.Date ->
                d.Value<System.DateTime>()
                |> KeyValue.Date
            | JTokenType.Boolean -> 
                d.Value<bool>()
                |> KeyValue.Bool 
            | JTokenType.Array ->
                d.AsJEnumerable()
                |> Seq.map keyValue
                |> List.ofSeq
                |> KeyValue.List
            | _ -> 
                d 
                |> KeyValue.JToken

        let parseViewDoc (d: JObject): ViewDoc = 
            // This id prop should not be mapped to a field
            let id = d.Value<string> "id"
            let keyToken = d.GetValue "key"
            let key = 
                match keyToken.Type with 
                | JTokenType.Null -> failwith "View doc's key was null"
                | JTokenType.Array -> 
                    keyToken.AsJEnumerable()
                    |> Seq.map keyValue
                    |> List.ofSeq 
                    |> ViewKey.KeyList
                | _ -> 
                    keyToken 
                    |> keyValue
                    |> ViewKey.Key

            let tokenToDoc (tokenName: string) = 
                tokenName 
                |> d.GetValue 
                |> fun token -> 
                    match isNull token with 
                    | true -> 
                        None 
                    | false -> 
                        match token.Type with 
                        | JTokenType.Null -> 
                            None
                        | _ ->
                            JsonToken token 
                            |> x.ReadAsJToken mapping 
                            |> fun (typeName, j) -> Document(typeName, j, defaultSerializer)
                            |> Some

            { Id = id; Key = key; Value = tokenToDoc "value"; Doc = tokenToDoc "doc" }

        let rows = 
            j.["rows"].AsJEnumerable()
            |> Seq.map ((fun x -> JObject.FromObject(x, defaultSerializer)) >> parseViewDoc)
            |> List.ofSeq

        { TotalRows = totalRows; Offset = offset; Rows = rows}

    override __.ReadAsPostPutCopyResponse mapping json = failwith "not implemented"

    override __.ReadAsFindResult mapping json = failwith "not implemented"

    override __.ReadAsBulkResultList json = failwith "not implemented"

    override x.ReadVersionToken json = 
        let _, doc = x.ReadAsJToken Map.empty (JsonString json)

        doc.Value<string> "version"