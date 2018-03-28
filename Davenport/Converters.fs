module Davenport.Converters

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Types
open Infrastructure

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

let makeDefaultSettings() = 
    let settings = Microsoft.FSharpLu.Json.Compact.Internal.Settings.settings
    settings.NullValueHandling <- NullValueHandling.Ignore
    settings.MissingMemberHandling <- MissingMemberHandling.Ignore
    settings

let makeDefaultSerializer (settings: JsonSerializerSettings) = 
    JsonSerializer.Create(settings)

type DefaultConverter (defaultSerializer: JsonSerializer, defaultSerializerSettings: JsonSerializerSettings) = 
    inherit ICouchConverter()

    let stringify o = JsonConvert.SerializeObject(o, defaultSerializerSettings)

    /// <summary>
    /// Stringifies the JsonKind back to JSON. This is intended mainly for logging.
    /// </summary>
    let stringifyJsonKind = function 
        | JsonParseKind.JsonObject o -> stringify o
        | JsonParseKind.JsonToken t -> stringify t
        | JsonParseKind.JsonString s -> s

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

    let (|PostPutCopyToken|_|) (token: JToken) = 
        // PostPutCopy objects have a .ok property 
        match isNull token.["ok"] with 
        | true -> None 
        | false -> 
            { Okay = token.Value<bool>("ok") 
              Id = token.Value<string>("id")
              Rev = token.Value<string>("rev") }
            |> Some

    let (|BulkErrorToken|_|) (token: JToken) =
        // Bulk document error objects have a .error property
        match isNull token.["error"] with 
        | true -> None 
        | false -> 
            let error = 
                match token.Value<string>("error") with 
                | "conflict" -> Conflict 
                | "forbidden" -> Forbidden 
                | s -> BulkErrorType.Other s

            let rev = 
                token.["rev"]
                |> Option.ofObj
                |> Option.map (fun t -> t.Value<string>())

            { Id = token.Value<string>("id")
              Error = error
              Reason = token.Value<string>("reason")
              Rev = rev }
            |> Some

    new() = 
        let settings = makeDefaultSettings()
        DefaultConverter(makeDefaultSerializer settings, settings)

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
            | IncludeDocs i::rest ->
                Map.add "include_docs" (string i) qs 
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
                    // Previously we'd throw an error here if the Id or Rev fields weren't found. However, this introduced a bug with the C# wrapper: 
                    // the Id and Rev fields of C# classes can be null, and our default serializer settings is configured to ignore null values. Therefore,
                    // the Id and Rev JTokens would be null and make that error throw whenever the Id and Rev values were themselves null. 
                    // Instead we just skip of them and continue serializing.
                    None

                    // sprintf "%s field '%s' was not found on type %s. If you want to map it to a custom field on your type, use Davenport's `converterSettings` function to pass a list of field mappings." readableFieldName givenFieldName docValueType.FullName
                    // |> System.ArgumentException
                    // |> raise
                | false ->
                    let value = j.[givenFieldName].Value<string>()

                    match System.String.IsNullOrEmpty value with 
                    | true -> None 
                    | false ->
                        Some (StringProp (canonFieldName, value)))

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
        // Desired json looks like { "name" : "index-name", "index": {fields": ["field1", "field2", "field3"]}}
        let fieldProp = ArrayProp("fields", fields |> List.map JsonValue.String)

        yield StringProp ("name", name)
        yield ObjectProp ("index", [fieldProp])
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
            | UseIndex (UseIndex.FromDesignDoc i)::rest ->
                getOptions rest (out@[StringProp("use_index", i)])
            | UseIndex (UseIndex.FromDesignDocAndIndex (docId, indexName))::rest ->
                let index = [
                    JsonValue.String docId
                    JsonValue.String indexName
                ]

                getOptions rest (out@[ArrayProp("use_index", index)])
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
        let (typeName, j) = x.ReadAsJToken mapping json

        Document(typeName, j, defaultSerializer)

    override x.ReadAsViewResult mapping json = 
        let (_, j) = x.ReadAsJToken mapping json
        let offset = j.Value<int>("offset")
        let totalRows = j.Value<int>("total_rows")
        
        let rec toViewKey (d: JToken): ViewKey = 
            match d.Type with 
            | JTokenType.Null -> 
                ViewKey.Null
            | JTokenType.String -> 
                d.Value<string>()
                |> ViewKey.String 
            | JTokenType.Integer ->
                let str = d.Value<string>()

                // First try to parse the value as an Int. If it's too long it will return None and we'll parse as an int64 instead.
                // If both parses fail, fall back to returning the value as a string.
                str 
                |> Int.parse
                |> Option.map ViewKey.Int
                |> Option.defaultBindWith(fun _ ->
                    str 
                    |> Long.parse 
                    |> Option.map ViewKey.Long)
                |> Option.defaultValue (ViewKey.String str)
            | JTokenType.Float ->
                d.Value<float>()
                |> ViewKey.Float 
            | JTokenType.Date ->
                d.Value<System.DateTime>()
                |> ViewKey.Date
            | JTokenType.Boolean -> 
                d.Value<bool>()
                |> ViewKey.Bool 
            | JTokenType.Array ->
                d.AsJEnumerable()
                |> Seq.map toViewKey
                |> List.ofSeq
                |> ViewKey.List
            | _ -> 
                d 
                |> ViewKey.JToken

        let parseViewDoc (d: JObject): ViewDoc = 
            // This id prop should not be mapped to a field
            let id = d.Value<string> "id"
            let keyToken = d.GetValue "key"
            let key = 
                match keyToken.Type with 
                | JTokenType.Null -> failwith "View doc's key was null"
                | JTokenType.Array -> 
                    keyToken.AsJEnumerable()
                    |> Seq.map toViewKey
                    |> List.ofSeq 
                    |> ViewKey.List
                | _ -> 
                    keyToken 
                    |> toViewKey

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
                            |> x.ReadAsDocument mapping
                            |> Some

            { Id = id; Key = key; Value = tokenToDoc "value"; Doc = tokenToDoc "doc" }

        let rows = 
            j.["rows"].AsJEnumerable()
            |> Seq.map ((fun x -> JObject.FromObject(x, defaultSerializer)) >> parseViewDoc)
            |> List.ofSeq

        { TotalRows = totalRows; Offset = offset; Rows = rows}

    override x.ReadAsPostPutCopyResponse json =
        let _, token = x.ReadAsJToken Map.empty json

        match token with 
        | PostPutCopyToken t -> t
        | _ -> failwithf "Failed to read json as PostPutCopyResponse. %s" (stringifyJsonKind json)

    override x.ReadAsFindResult mapping json = 
        let _, token = x.ReadAsJToken mapping json 

        let docs = 
            token.["docs"]
            |> Option.ofObj 
            |> Option.map (fun t -> t.AsJEnumerable() |> List.ofSeq)
            |> Option.defaultValue []
            |> Seq.map (JsonToken >> x.ReadAsDocument mapping)
            |> List.ofSeq

        let warning = 
            token.["warning"]
            |> Option.ofObj 
            |> Option.map (fun t -> t.Value<string>())

        warning, docs

    override x.ReadAsBulkResultList json = 
        x.ReadAsJToken Map.empty json
        |> fun (_, token) -> token.AsJEnumerable()
        |> Seq.map (function 
            | PostPutCopyToken t -> BulkResult.Inserted t
            | BulkErrorToken t -> BulkResult.Failed t
            | t -> failwithf "Failed to read json array element as a BulkResult. %s" (stringify t))
        |> List.ofSeq

    override x.ReadVersionToken json = 
        let _, doc = x.ReadAsJToken Map.empty json

        doc.Value<string> "version"

    override x.ReadAsIndexInsertResult json = 
        let _, doc = x.ReadAsJToken Map.empty json

        let result = 
            match doc.Value<string>("result") with 
            | "created" -> CreateResult.Created
            | "exists" -> CreateResult.AlreadyExisted
            | s -> failwithf "Could not deserialize unknown doc.result value \"%s\"." s
        
        { Id = doc.Value<string>("id")
          Name = doc.Value<string>("name")
          Result = result }