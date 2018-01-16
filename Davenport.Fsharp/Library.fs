module Davenport.Fsharp

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Linq

type CouchProps = private {
    username: string option
    password: string option
    converter: JsonConverter option
    databaseName: string
    couchUrl: string
    docType: System.Type
    id: string
    rev: string
}

let private defaultCouchProps() = {
        username = None
        password = None
        converter = None
        databaseName = ""
        couchUrl = ""
        docType = typeof<obj>
        id = "_id"
        rev = "_rev"
    }

/// This type is combined with the custom json converter to allow consumers of this package to pass any F# record type to Davenport without turning their records into classes that inherit couchdoc.
type private FsDoc() =
    inherit Davenport.Entities.CouchDoc()
    member val Data: obj option = None with get,set

type private MyRandoType = { MyId: string; MyRev: string; Foo: bool; Bar: int }

type FsConverter<'doctype>(idField: string, revField: string, docType: System.Type) =
    inherit JsonConverter()
    let fableConverter = Fable.JsonConverter() :> JsonConverter
    override __.CanConvert objectType = objectType = typeof<FsDoc>

    override __.ReadJson(reader: JsonReader, objectType: System.Type, existingValue: obj, serializer: JsonSerializer) =
        let j = JObject.Load reader
        let id: JToken option = Option.ofObj j.["_id"]
        let rev: JToken option = Option.ofObj j.["_rev"]

        id
        |> Option.iter (fun value ->
            // Rename _id to idField
            if idField <> "_id" then
                j.Remove "_id" |> ignore
                j.Add(idField, value)
        )

        rev
        |> Option.iter (fun value ->
            // Rename _rev to revField
            if revField <> "_rev" then
                j.Remove "_rev" |> ignore
                j.Add(revField, value)
        )

        let data = j.ToObject(docType, serializer)
        let output = FsDoc()
        output.Id <-
            id
            |> Option.bind (fun id -> id.Value<string>() |> Some)
            |> Option.defaultValue ""
        output.Rev <-
            rev
            |> Option.bind (fun rev -> rev.Value<string>() |> Some)
            |> Option.defaultValue ""
        output.Data <- data |> Some

        output :> obj

    override __.WriteJson(writer: JsonWriter, objValue: obj, serializer: JsonSerializer) =
        if isNull objValue
        then serializer.Serialize(writer, null)
        else

        let doc = objValue :?> FsDoc
        writer.WriteStartObject()

        // Load the data object into a JObject
        let j = JObject.FromObject doc.Data

        // Find the data object's id and rev fields.
        // A JObject field will be null if it doesn't exist, but will return a JToken with Null value if the field does exist and it's null.
        let id =
            if isNull j.[idField]
            then sprintf "Id field '%s' was not found on type %s." idField docType.FullName |> System.ArgumentException |> raise
            else j.[idField]

        let rev =
            if isNull j.[revField]
            then sprintf "Rev field '%s' was not found on type %s." revField docType.FullName |> System.ArgumentException |> raise
            else j.[revField]

        // Write the _id and _rev values if they aren't null or empty. Writing either one when it isn't intended can make CouchDB throw an error.
        [id, "_id"; rev, "_rev"]
        |> Seq.iter (fun (token, name) ->
            let value = token.Value<string>()

            if System.String.IsNullOrEmpty value |> not then
                writer.WritePropertyName name
                writer.WriteValue value
        )

        // Merge the FsDoc's data property with the doc being written so they're at the same level.
        Seq.cast<JProperty> j
        |> Seq.filter (fun prop -> prop.Name <> idField && prop.Name <> revField)
        |> Seq.iter (fun prop -> prop.WriteTo(writer, [|fableConverter|]))

        // // Merge the FsDoc's data property with the doc being written so they're at the same level.
        // doc.Data.GetType().GetProperties()
        // |> Seq.filter (fun prop -> prop.Name <> idField && prop.Name <> revField)
        // |> Seq.iter (fun prop ->
        //     writer.WritePropertyName(prop.Name)
        //     // Let the serializer figure out how to serialize the property value
        //     serializer.Serialize(writer, prop.GetValue(doc.Data, null))
        // )

        writer.WriteEndObject()

let database name couchUrl =
    { defaultCouchProps() with databaseName = name; couchUrl = couchUrl }

let username username config = { config with username = Some username }

let password password config = { config with password = Some password }

let idName name props = { props with id = name }

let revName name props = { props with rev = name }

let docType docType props = { props with docType = docType }

let converter converter props = { props with converter = Some converter }

let private toConfig (props: CouchProps) =
    let config = Davenport.Configuration(props.couchUrl, props.databaseName)
    config.Username <- Option.defaultValue "" props.username
    config.Password <- Option.defaultValue "" props.password
    config.Converter <- Option.defaultWith (fun _ -> FsConverter(props.id, props.rev, props.docType) :> JsonConverter) props.converter
    config

let private mapFsDocRowTo<'a> row: Entities.ListedRow<'a> =
    Entities.ListedRow<'a>()

let getCouchVersion props =
    toConfig props
    |> Davenport.Configuration.GetVersionAsync
    |> Async.AwaitTask

let isVersion2OrAbove = Davenport.Configuration.IsVersion2OrAbove

/// Creates a CouchDB database if it doesn't exist.
let createDatabase props =
    toConfig props
    |> Davenport.Configuration.CreateDatabaseAsync
    |> Async.AwaitTask

/// Creates the given design docs. Will check that each view in each design doc has functions that perfectly match the ones found in the database, and update them if they don't match.
/// Will throw an ArgumentException if no design docs are given.
let createDesignDocs (docs: Davenport.Entities.DesignDocConfig seq) props =
    let config = toConfig props

    Davenport.Configuration.CreateDesignDocsAsync(config, docs)
    |> Async.AwaitTask

/// Creates indexes for the given fields. This makes querying with the Find methods and selectors faster.
/// Will throw an ArgumentException if no indexes are given.
let createIndexes (indexes: string seq) props =
    let config = toConfig props

    Davenport.Configuration.CreateDatabaseIndexesAsync(config, indexes)
    |> Async.AwaitTask

/// Combines the createDatabase, createDesignDocs and createIndexes functions, running all three at once.
let configure (designDocs: Davenport.Entities.DesignDocConfig seq) (indexes: string seq) props = async {
    let config = toConfig props

    do!
        Davenport.Configuration.ConfigureDatabaseAsync<FsDoc>(config, indexes, designDocs)
        |> Async.AwaitTask
        |> Async.Ignore
}

let create data props =
    let client = toConfig props |> Davenport.Client<FsDoc>
    let doc = FsDoc()
    doc.Data <- Some data

    client.PostAsync doc
    |> Async.AwaitTask

let get id (rev: string option) props = async {
    let client = toConfig props |> Davenport.Client<FsDoc>
    let! doc =
        client.GetAsync(id, Option.toObj rev)
        |> Async.AwaitTask
        |> Async.Catch

    return
        match doc with
        | Choice1Of2 doc -> Some doc.Data
        | Choice2Of2 _ -> None
}

let listWithDocs (listOptions: Entities.ListOptions option) props = async {
    let client = toConfig props |> Davenport.Client<FsDoc>
    let options = Option.toObj listOptions

    let! result =
        client.ListWithDocsAsync(options)
        |> Async.AwaitTask
    let newRows =
        result.Rows
        |> Seq.map mapFsDocRowTo

    return ""
}

let connection =
   "localhost:5984"
    |> database "henlo_world"
    |> docType typeof<MyRandoType>
    |> idName "MyId"
    |> revName "MyRev"