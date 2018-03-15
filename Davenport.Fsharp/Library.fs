module Davenport.Fsharp

open Davenport.Infrastructure
open Davenport.Converters
open Davenport.Types

let private defaultProps =  
    { username = None 
      password = None 
      converter = DefaultConverter()
      databaseName = ""
      couchUrl = ""
      onWarning = Event<string>()
      fieldMapping = Map.empty }

let database name couchUrl =
    { defaultProps with databaseName = name; couchUrl = couchUrl }

let username username config = { config with username = Some username }

let password password config = { config with password = Some password }

let converter converter props = { props with converter = converter }

/// <summary>
/// Map custom field names to the CouchDB _id and _rev fields. This can be used multiple times. Any key that is already set and also exists in the new mapping will be overwritten.
/// </summary>
let mapFields mapping (props: CouchProps) = 
    // Merge the new mapping into the old one, overwriting old keys if necessary.
    let newMapping = Map.fold (fun state key value -> Map.add key value state) props.fieldMapping mapping
    { props with fieldMapping = newMapping }

let warning handler (props: CouchProps) = 
    Event.add handler props.onWarning.Publish
    props

let getRaw id rev props = 
    request id props
    |> querystring (props.converter.ConvertRevToMap rev)
    |> send Get

let get id rev props = 
    getRaw id rev props
    |> Async.Map (props.converter.ReadAsDocument props.fieldMapping)

let allDocsRaw includeDocs options props = 
    let includeDocs = 
        match includeDocs with 
        | WithDocs -> true
        | WithoutDocs -> false
        |> string 
        |> String.Lowercase

    let qs = 
        options
        |> props.converter.ConvertListOptionsToMap
        |> Map.add "include_docs" (string includeDocs)

    props
    |> request "_all_docs"
    |> querystring qs
    |> send Get

let allDocs includeDocs options props = 
    allDocsRaw includeDocs options props 
    |> Async.Map (props.converter.ReadAsViewResult props.fieldMapping)

let create (document: InsertedDocument<'a>) props = 
    request "" props
    |> body (props.converter.WriteInsertedDocument props.fieldMapping document)
    |> send Post
    |> Async.Map (props.converter.ReadAsPostPutCopyResponse props.fieldMapping)

let createWithId id (document: InsertedDocument<'a>) props = 
    request id props
    |> body (props.converter.WriteInsertedDocument props.fieldMapping document)
    |> send Put
    |> Async.Map (props.converter.ReadAsPostPutCopyResponse props.fieldMapping)

let update id rev (document: InsertedDocument<'a>) props = 
    match rev with 
    | None -> 
        request id props 
    | Some rev -> 
        request id props 
        |> querystring (props.converter.ConvertRevToMap rev)
    |> body (props.converter.WriteInsertedDocument props.fieldMapping document)
    |> send Put
    |> Async.Map (props.converter.ReadAsPostPutCopyResponse props.fieldMapping)

let exists id rev props =
    match rev with 
    | None ->
        request id props 
    | Some rev -> 
        request id props 
        |> querystring (props.converter.ConvertRevToMap rev)
    |> send Head
    |> Async.Catch
    // Doc exists if CouchDB didn't throw an error status code
    |> Async.Map (function | Choice1Of2 _ -> true | Choice2Of2 _ -> false) 

let copy oldId newId props = 
    request oldId props
    |> headers (Map.ofSeq ["Destination", newId])
    |> send Copy
    |> Async.Map (props.converter.ReadAsPostPutCopyResponse props.fieldMapping)

let delete id rev props = 
    match rev with 
    | None -> 
        request id props 
    | Some rev -> 
        request id props
        |> querystring (props.converter.ConvertRevToMap rev)
    |> send Delete
    |> Async.Ignore

/// <summary>
/// Queries a view and returns the unparsed JSON string. 
/// NOTE: This function forces the `reduce` parameter to FALSE, i.e. it will NOT reduce. Use the `reduce` or `reduceRaw` functions instead.
/// </summary>
let viewRaw designDocName viewName options props = 
    (sprintf "_design/%s/_view/%s" designDocName viewName, props)
    ||> request
    |> querystring (props.converter.ConvertListOptionsToMap options |> Map.add "reduce" "false")
    |> send Get

/// <summary>
/// Queries a view. 
/// NOTE: This function forces the `reduce` parameter to FALSE, i.e. it will NOT reduce. Use the `reduce` or `reduceRaw` functions instead.
/// </summary>
let view designDocName viewName options props = 
    props
    |> viewRaw designDocName viewName options
    |> Async.Map (props.converter.ReadAsViewResult props.fieldMapping)

/// <summary>
/// Queries a view and reduces it, returning the raw JSON string.
/// NOTE: This function forces the `reduce` parameter to TRUE< i.e. will ALWAYS reduce. Use the `view` or `ViewRaw` functions to query a view's docs instead.
/// </summary>
let reduceRaw designDocName viewName options props = 
    (sprintf "_design/%s/_view/%s" designDocName viewName, props)
    ||> request
    |> querystring (props.converter.ConvertListOptionsToMap options |> Map.add "reduce" "true")
    |> send Get

/// <summary>
/// Queries a view and reduces it.
/// NOTE: This function forces the `reduce` parameter to TRUE< i.e. will ALWAYS reduce. Use the `view` or `ViewRaw` functions to query a view's docs instead.
/// </summary>
let reduce designDocName viewName options props = 
    props 
    |> reduceRaw designDocName viewName options
    |> Async.Map (props.converter.ReadAsDocument props.fieldMapping)

let findRaw findOptions selector props =
    let data = 
        findOptions
        |> props.converter.ConvertFindOptionsToMap
        |> Map.map (fun _ value -> value :> obj)
        |> Map.add "selector" (props.converter.ConvertFindSelectorToMap selector :> obj)

    props 
    |> request "_find"
    |> body (props.converter.WriteUnknownObject data)
    |> send Get

/// <summary>
/// Searches for documents matching the given selector.
/// </summary>
let find (findOptions: FindOption list) selector props = async {
    let! (warning, docs) = 
        findRaw findOptions selector props
        |> Async.Map (props.converter.ReadAsFindResult props.fieldMapping)

    Option.iter props.onWarning.Trigger warning

    return docs
}

let count = 
    allDocs WithoutDocs [ListLimit 0] 
    >> Async.Map (fun (totalRows, _, _) -> totalRows)

/// <summary>
/// Retrieves a count of all documents matching the given selector.
/// NOTE: Internally this uses the Find API and may be slower than normal operations. Performance may be improved by using indexes.
/// </summary>
let countBySelector selector = 
    // Selectors must use the Find API, which means they must return documents too. Limit the bandwidth by just returning _id.
    find [Fields ["_id"]] selector
    >> Async.Map Seq.length

/// <summary>
/// Checks that a document matching the given selector exists.
/// NOTE: Internally this uses the Find API and may be slower than normal operations. Performance may be improved by using indexes.
/// </summary>
let existsBySelector selector = 
    // Selectors must use the Find API, which means they must return documents too. Limit the bandwidth by just returning _id.
    find [Fields ["_id"]] selector
    >> Async.Catch
    // Doc exists if CouchDB didn't throw an error status code
    >> Async.Map (function | Choice1Of2 _ -> true | Choice2Of2 _ -> false) 

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
let bulkInsert mode (docs: InsertedDocument<'a> list) props = 
    let qs = 
        match mode with 
        | AllowNewEdits -> Map.ofSeq ["new_edits", "true"]
        | NoNewEdits -> Map.empty
    
    props    
    |> request "_bulk_docs"
    |> querystring qs
    |> body (props.converter.WriteBulkInsertList props.fieldMapping docs)
    |> send Post
    |> Async.Map props.converter.ReadAsBulkResultList

let getCouchVersion props =
    request "" props
    |> send Get 
    |> Async.Map (props.converter.ReadAsJToken props.fieldMapping >> fun t -> t.Value<string> "version" )

let isVersion2OrAbove = 
    getCouchVersion
    >> Async.Map (fun str ->
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
    |> Async.Map (
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
    >> Async.Ignore

/// <summary>
/// Creates the given design docs. This is a dumb function and will overwrite the data of any design doc that shares its id.
/// </summary>
let createOrUpdateDesignDoc ((id, views): DesignDoc) props =
    request (sprintf "_design/%s" id) props
    |> body (props.converter.WriteDesignDoc views)
    |> send Put
    |> Async.Ignore

/// <summary>
/// Creates indexes for the given fields. This makes querying with the Find methods and selectors faster.
/// Will throw an ArgumentException if no indexes are given.
/// </summary>
let createIndexes (fields: IndexField list) props =
    let name = sprintf "%s-indexes" props.databaseName
        
    props
    |> request "_index"
    |> body (props.converter.WriteIndexes name fields)
    |> send Post

module DesignDoc =
    let doc name views: DesignDoc = name, views

    let addView viewName mapFunc revFunc ((name, previousViews): DesignDoc): DesignDoc = 
        name, previousViews |> Map.add viewName (mapFunc, revFunc)

    let addViews views ((name, previousViews): DesignDoc): DesignDoc = 
        name, (Map.fold (fun state key value -> Map.add key value state) previousViews views)