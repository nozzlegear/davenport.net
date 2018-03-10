module Davenport.Fsharp.Wrapper

open Davenport.Fsharp.Infrastructure
open Davenport.Fsharp.Converters
open Davenport.Fsharp.Types
open Newtonsoft.Json.Linq

// let private toConfig<'doctype> (props: CouchProps) =
//     let config = Davenport.Configuration(props.couchUrl, props.databaseName)
//     config.Username <- Option.defaultValue "" props.username
//     config.Password <- Option.defaultValue "" props.password
//     config.Converter <- Option.defaultWith (fun () -> FsConverter<'doctype>(props.id, props.rev, None) :> JsonConverter) props.converter

//     props.onWarning
//     |> Option.iter (fun handler -> handler config.Warning)

//     config

// let private toClient<'doctype> = toConfig<'doctype> >> Davenport.Client<FsDoc<'doctype>>

let private defaultProps =  
    { username = None 
      password = None 
      converter = FsConverter []
      databaseName = ""
      couchUrl = ""
      id = "_id"
      rev = "_rev" 
      onWarning = Event<string>() }

let database name couchUrl =
    { defaultProps with databaseName = name; couchUrl = couchUrl }

let username username config = { config with username = Some username }

let password password config = { config with password = Some password }

let idField name props = { props with id = name }

let revField name props = { props with rev = name }

let converter converter props = { props with converter = converter }

let warning handler (props: CouchProps) = 
    Event.add handler props.onWarning.Publish
    props

let mapDoc (fn: Document) (computation: Async<string>) = 
    // This will receive the async task from e.g. `get`, plus accept an arbitrary function that will
    // receive a (typeName: string -> jtoken -> useDefaultDeserializer: unit -> 'a)
    ""

let mapDocList (fn: DocumentList) (computation: Async<string>) = 
    ""

let getRaw id rev = 
    request id
    >> querystring (qsFromRev rev)
    >> send Get

let get id rev props = 
    getRaw id rev props
    |> asyncMap (stringToDocument props.converter)

let allDocsRaw includeDocs options = 
    let includeDocs = 
        match includeDocs with 
        | WithDocs -> true
        | WithoutDocs -> false

    let qs = 
        qsFromListOptions options
        |> Map.add "include_docs" (includeDocs :> obj)

    request "_all_docs"
    >> querystring qs
    >> send Get

let allDocs includeDocs options props = 
    allDocsRaw includeDocs options props 
    |> asyncMap (stringToDocumentList props.converter)

let count = 
    allDocs WithoutDocs [Limit 0] 
    >> asyncMap (fun (totalRows, _, _) -> totalRows)

let countByExpression () = failwith "Not implemented"

let countByObject () = failwith "Not implemented"

let countBySelector () = failwith "Not implemented"

let create document props = 
    request "" props
    |> body document
    |> send Post
    |> asyncMap (stringToPostPutCopyResponse props.converter)

let createWithId id document props = 
    request id props
    |> body document 
    |> send Put
    |> asyncMap (stringToPostPutCopyResponse props.converter)

let update id rev document props = 
    request id props
    |> querystring (qsFromRev rev)
    |> body document
    |> send Put
    |> asyncMap (stringToPostPutCopyResponse props.converter)

let exists id rev =
    request id 
    >> querystring (qsFromRev rev)
    >> send Head
    >> Async.Catch
    // Doc exists if CouchDB didn't throw an error status code
    >> asyncMap (function | Choice1Of2 _ -> true | Choice2Of2 _ -> false) 

let existsByExpression () = failwith "Not implemented"

let existsByObject () = failwith "Not implemneted"

let existsBySelector () = failwith "Not implemented"

let copy oldId newId props = 
    request oldId props
    |> headers (Map.ofSeq ["Destination", newId])
    |> send Copy
    |> asyncMap (stringToPostPutCopyResponse props.converter)

let delete id rev (props: CouchProps) = 
    if Option.isNone rev
    then props.onWarning.Trigger <| sprintf "No revision given for delete method with id %s. This may cause a document conflict error." id

    request id props
    |> querystring (qsFromRev rev)
    |> send Delete
    |> asyncMap ignore

let view designDocName viewName options props = failwith "Not implemented"

let private findByDictionary<'doctype> selector (findOptions: FindOptions option) props =
    let client = toClient<'doctype> props
    let options = Option.toObj findOptions

    client.FindBySelectorAsync (selector, options)
    |> Async.AwaitTask
    |> asyncMapSeq (fun doc -> Option.get doc.Data)

/// Searches for documents matching the given selector.
let findBySelector<'doctype> = convertMapToDict >> findByDictionary<'doctype>

/// Searches for documents matching the given selector.
/// Usage: findByExpr<DocType> (<@ fun (c: DocType) -> c.SomeProp = SomeValue @>)
/// NOTE: Davenport currently only supports simple 1 argument selectors.
let findByExpr<'doctype> = convertExprToMap<'doctype> >> findByDictionary<'doctype>

let private countByDictionary selector props =
    let client = toClient props

    client.CountBySelectorAsync selector
    |> Async.AwaitTask

/// Retrieves a count of all documents matching the given selector.
let countBySelector = convertMapToDict >> countByDictionary

/// Retrieves a count of all documents matching the given selector.
/// Usage: countByExpr<DocType> (<@ fun (c: DocType) -> c.SomeProp = SomeValue @>)
/// NOTE: Davenport currently only supports simple 1 argument selectors.
let countByExpr<'doctype> = convertExprToMap<'doctype> >> countByDictionary

let private existsByDictionary selector props =
    let client = toClient props

    client.ExistsBySelectorAsync selector
    |> Async.AwaitTask

/// Checks that a document matching the given selector exists.
let existsBySelector = convertMapToDict >> existsByDictionary

/// Checks that a document matching the given selector exists.
/// Usage: existsByExpr<DocType> (<@ fun (c: DocType) -> c.SomeProp = SomeValue @>)
/// NOTE: Davenport currently only supports simple 1 argument selectors.
let existsByExpr<'doctype> = convertExprToMap<'doctype> >> existsByDictionary

let bulkInsert () = failwith "Not implemented"

let getCouchVersion props =
    request "" props
    |> send Get 
    |> asyncMap (ofJson<JToken> props.converter >> fun t -> t.Value<string> "version" )

let isVersion2OrAbove = 
    getCouchVersion
    >> asyncMap (fun str ->
        let version = System.Convert.ToInt32(str.Split('.').[0])

        version >= 2
    )

/// <summary>
/// Creates a CouchDB database if it doesn't exist.
/// </summary>
let createDatabase props =
    request "" props
    |> send Put 
    |> Async.Catch
    |> asyncMap (
        function
        | Choice1Of2 _ -> Created
        | Choice2Of2 (:? DavenportException as exn) when exn.StatusCode = 412 -> AlreadyExisted
        | Choice2Of2 exn -> raise exn
    )

/// <summary>
/// Deletes the database. This cannot be undone!
/// </summary>
let deleteDatabase =
    request ""
    >> send Delete
    >> asyncMap ignore

/// <summary>
/// Creates the given design docs. This is a dumb function and will overwrite the data of any design doc that shares its id.
/// </summary>
let createOrUpdateDesignDoc ((id, views): DesignDoc) props =
    let viewData = 
        views
        |> Seq.map (fun (name, map, reduce) ->
            match reduce with 
            | None -> Map.empty
            | Some reduce -> Map.add "reduce" reduce Map.empty
            |> Map.add "name" name
            |> Map.add "map" map
            |> fun view -> name, view
        )
        |> Map.ofSeq

    let data = 
        [
            "views", viewData :> obj
            // Javascript is currently the only supported language
            "language", "javascript" :> obj
        ]
        |> Map.ofSeq

    request (sprintf "_design/%s" id) props
    |> body data
    |> send Put
    |> asyncMap ignore

/// <summary>
/// Creates indexes for the given fields. This makes querying with the Find methods and selectors faster.
/// Will throw an ArgumentException if no indexes are given.
/// </summary>
let createIndexes (indexes: string seq) props =
    let indexData =
        [
            "name", (sprintf "%s-indexes" props.databaseName) :> obj
            "fields", (Map.ofSeq [ "fields", indexes ]) :> obj
        ]
        |> Map.ofSeq
        
    request "_index" props
    |> body indexData
    |> send Post