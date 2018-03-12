module Davenport.Fsharp

open Davenport.Infrastructure
open Davenport.Converters
open Davenport.Types
open Newtonsoft.Json.Linq

let private defaultProps =  
    { username = None 
      password = None 
      converter = DefaultConverter Map.empty
      databaseName = ""
      couchUrl = ""
      onWarning = Event<string>() }

let database name couchUrl =
    { defaultProps with databaseName = name; couchUrl = couchUrl }

let username username config = { config with username = Some username }

let password password config = { config with password = Some password }

let converter converter props = 
    failwith "TODO: Use the converter.CanConvert function to check that the converter implements all necessary conversions."
    { props with converter = converter }

/// <summary>
/// Map custom field names to the CouchDB _id and _rev fields. This can be used multiple times. Any key that is already set and also exists in the new mapping will be overwritten.
/// </summary>
let mapFields mapping (props: CouchProps) = 
    props.converter.AddFieldMappings mapping
    props

let warning handler (props: CouchProps) = 
    Event.add handler props.onWarning.Publish
    props

let getRaw id rev = 
    request id
    >> querystring (mapFromRev rev)
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
        mapFromListOptions options
        |> Map.add "include_docs" (includeDocs :> obj)

    request "_all_docs"
    >> querystring qs
    >> send Get

let allDocs includeDocs options props = 
    allDocsRaw includeDocs options props 
    |> asyncMap (stringToDocumentList props.converter)

let create (document: InsertedDocument<'a>) props = 
    request "" props
    |> body document
    |> send Post
    |> asyncMap (stringToPostPutCopyResponse props.converter)

let createWithId id (document: InsertedDocument<'a>) props = 
    request id props
    |> body document 
    |> send Put
    |> asyncMap (stringToPostPutCopyResponse props.converter)

let update id rev (document: InsertedDocument<'a>) props = 
    request id props
    |> querystring (mapFromRev (Some rev))
    |> body document
    |> send Put
    |> asyncMap (stringToPostPutCopyResponse props.converter)

let exists id rev =
    request id 
    >> querystring (mapFromRev rev)
    >> send Head
    >> Async.Catch
    // Doc exists if CouchDB didn't throw an error status code
    >> asyncMap (function | Choice1Of2 _ -> true | Choice2Of2 _ -> false) 

let copy oldId newId props = 
    request oldId props
    |> headers (Map.ofSeq ["Destination", newId])
    |> send Copy
    |> asyncMap (stringToPostPutCopyResponse props.converter)

let delete id rev = 
    request id
    >> querystring (mapFromRev (Some rev))
    >> send Delete
    >> asyncMap ignore

/// <summary>
/// Queries a view and returns the unparsed JSON string. 
/// NOTE: This function forces the `reduce` parameter to FALSE, i.e. it will NOT reduce. Use the `reduce` or `reduceRaw` functions instead.
/// </summary>
let viewRaw designDocName viewName options props = 
    (sprintf "_design/%s/_view/%s" designDocName viewName, props)
    ||> request
    |> querystring (mapFromListOptions options |> Map.add "reduce" (false :> obj))
    |> send Get

/// <summary>
/// Queries a view. 
/// NOTE: This function forces the `reduce` parameter to FALSE, i.e. it will NOT reduce. Use the `reduce` or `reduceRaw` functions instead.
/// </summary>
let view designDocName viewName options props = 
    viewRaw designDocName viewName options props
    |> asyncMap (ofJson<JToken> props.converter >> fun token -> token.SelectToken("rows").ToObject<ViewDoc list>())

/// <summary>
/// Queries a view and reduces it, returning the raw JSON string.
/// NOTE: This function forces the `reduce` parameter to TRUE< i.e. will ALWAYS reduce. Use the `view` or `ViewRaw` functions to query a view's docs instead.
/// </summary>
let reduceRaw designDocName viewName options props = 
    (sprintf "_design/%s/_view/%s" designDocName viewName, props)
    ||> request
    |> querystring (mapFromListOptions options |> Map.add "reduce" (true :> obj))
    |> send Get

/// <summary>
/// Queries a view and reduces it.
/// NOTE: This function forces the `reduce` parameter to TRUE< i.e. will ALWAYS reduce. Use the `view` or `ViewRaw` functions to query a view's docs instead.
/// </summary>
let reduce designDocName viewName options props = 
    reduceRaw designDocName viewName options props
    |> asyncMap (stringToDocument props.converter)

let findRaw findOptions selector =
    let data = 
        mapFromFindOptions findOptions
        |> Map.add "selector" (convertFindsToMap selector :> obj)

    request "_find"
    >> body data
    >> send Get

/// <summary>
/// Searches for documents matching the given selector.
/// </summary>
let find (findOptions: FindOption list) selector props = async {
    let! (warning, docs) = 
        findRaw findOptions selector props
        |> asyncMap (stringToFoundList props.converter)

    Option.iter props.onWarning.Trigger warning

    return docs
}

let count = 
    allDocs WithoutDocs [ListLimit 0] 
    >> asyncMap (fun (totalRows, _, _) -> totalRows)

/// <summary>
/// Retrieves a count of all documents matching the given selector.
/// NOTE: Internally this uses the Find API and may be slower than normal operations. Performance may be improved by using indexes.
/// </summary>
let countBySelector selector = 
    // Selectors must use the Find API, which means they must return documents too. Limit the bandwidth by just returning _id.
    find [Fields ["_id"]] selector
    >> asyncMap Seq.length

/// <summary>
/// Checks that a document matching the given selector exists.
/// NOTE: Internally this uses the Find API and may be slower than normal operations. Performance may be improved by using indexes.
/// </summary>
let existsBySelector selector = 
    // Selectors must use the Find API, which means they must return documents too. Limit the bandwidth by just returning _id.
    find [Fields ["_id"]] selector
    >> Async.Catch
    // Doc exists if CouchDB didn't throw an error status code
    >> asyncMap (function | Choice1Of2 _ -> true | Choice2Of2 _ -> false) 

/// <summary>
/// Inserts, updates or deletes multiple documents at the same time. 
/// 
/// Omitting the id property from a document will cause CouchDB to generate the id itself.
/// 
/// When updating a document, the `_rev` property is required.
/// 
/// To delete a document, set the `_deleted` property to `true`. 
/// 
/// Note that CouchDB will return in the response an id and revision for every document passed as content to a bulk insert, even for those that were just deleted.  
/// 
/// If the `_rev` does not match the current version of the document, then that particular document will not be saved and will be reported as a conflict, but this does not prevent other documents in the batch from being saved. 
/// 
/// If the new edits are *not* allowed (to push existing revisions instead of creating new ones) the response will not include entries for any of the successful revisions (since their rev IDs are already known to the sender), only for the ones that had errors. Also, the `"conflict"` error will never appear, since in this mode conflicts are allowed. 
/// </summary>
let bulkInsert mode (docs: InsertedDocument<_> list) props = 
    let qs = 
        match mode with 
        | AllowNewEdits -> Map.ofSeq ["new_edits", true :> obj]
        | NoNewEdits -> Map.empty
    
    request "_bulk_docs" props
    |> querystring qs
    |> body (Map.ofSeq ["new_edits", docs :> obj])
    |> send Post
    |> asyncMap (stringToBulkResponseList props.converter)

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