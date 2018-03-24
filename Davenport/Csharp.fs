module Davenport.Csharp

open Davenport.Fsharp 
open Davenport.Types 
open Davenport.Infrastructure
open Davenport.Converters
open System
open System.Threading.Tasks
open Newtonsoft.Json
open System.Collections.Generic

module Types = 

    [<AbstractClass>]
    type CouchDoc() = 
        abstract member Id: string with get, set
        abstract member Rev: string with get, set

    type Revision(rev) = 
        new() = Revision("")
        member val Rev: string = rev with get, set

    type ListedRow<'doctype>(doc) = 
        member val Id = "" with get, set
        member val Key: obj = null with get, set
        member val Value: Revision = Revision("") with get, set
        member val Doc: 'doctype = doc with get, set

    type ListResponse<'doctype>() = 
        member val Offset = 0 with get, set
        member val TotalRows = 0 with get, set 
        member val Rows: IEnumerable<ListedRow<'doctype>> = Seq.empty with get, set
        member val DesignDocs: IEnumerable<ListedRow<obj>> = Seq.empty with get, set

    type ViewResponse<'value, 'doc>(key, value) = 
        member val Key: ViewKey = key with get, set 
        member val Value: 'value = value with get, set
        member val Doc: 'doc option = None with get, set

    type ViewConfig() = 
        member val Name: string = "" with get, set
        member val MapFunction: string = "" with get, set
        member val ReduceFunction: string = "" with get, set

    type DesignDocument() = 
        inherit CouchDoc()
        override val Id = "" with get, set
        override val Rev = "" with get, set
        member val Views: IEnumerable<ViewConfig> = Seq.empty with get, set

    type ListOptions() = 
        member val Limit = System.Nullable<int>() with get, set
        member val Key: obj = null with get, set
        member val Keys: obj seq = Seq.empty with get, set
        member val StartKey: obj = null with get, set
        member val EndKey: obj = null with get, set
        member val InclusiveEnd = System.Nullable<bool>() with get, set
        member val Descending = System.Nullable<bool>() with get, set
        member val Skip = System.Nullable<int>() with get, set
        member val Group = System.Nullable<bool>() with get, set
        member val GroupLevel = System.Nullable<int>() with get, set

    type FindExpression() = 
        member val EqualTo: obj = null with get, set
        member val NotEqualTo: obj = null with get, set
        member val GreaterThan: obj = null with get, set
        member val GreaterThanOrEqualTo: obj = null with get, set
        member val LesserThan: obj = null with get, set
        member val LesserThanOrEqualTo: obj = null with get, set

    type SortingOrder = 
        | Ascending 
        | Descending

    type Sorting(fieldName: string, order: SortingOrder) =
        member val FieldName = fieldName with get, set
        member val Order = order with get, set

    type UsableIndexType = 
        | FromDesignDoc
        | FromDesignDocAndIndex

    type UsableIndex (designDocId: string, ?indexName: string) =
        member val DesignDocId = designDocId with get, set
        member val IndexName = (indexName |> Option.defaultValue "") with get, set

    type FindOptions(?useIndex) = 
        member val Fields: string list = [] with get, set
        member val SortBy: Sorting list = [] with get, set
        member val Limit = System.Nullable<int>() with get, set
        member val UseIndex: UsableIndex option = useIndex with get, set

    type Configuration(couchUrl: string, databaseName: string) = 
        member val CouchUrl = couchUrl with get, set
        member val DatabaseName = databaseName with get, set
        member val Username = "" with get, set
        member val Password = "" with get, set
        member val JsonConverter: JsonConverter = null with get, set
        member val Warning: EventHandler<string> = null with get, set

open Types
open System.Collections

type Client<'doctype when 'doctype :> CouchDoc>(config: Configuration) = 
    let maybeAddConverter (converter: JsonConverter option) (props: CouchProps) =
        match converter with 
        | None -> props
        | Some j -> 
            // TODO: build jsonserializersettings, add the converter and pass it to default converter
            props

    let maybeAddWarning (evtHandler: EventHandler<string> option) (props: CouchProps) = 
        match evtHandler with 
        | None -> props 
        | Some handler -> props |> warning (fun s -> handler.Invoke(config, s))

    let typeName = 
        let t = typeof<'doctype>
        t.FullName

    let client = 
        config.CouchUrl
        |> database config.DatabaseName
        |> fun props -> match config.Username with | NotNullOrEmpty s -> username s props | _ -> props
        |> fun props -> match config.Password with | NotNullOrEmpty s -> password s props | _ -> props
        |> maybeAddConverter (Option.ofObj config.JsonConverter)
        |> maybeAddWarning (Option.ofObj config.Warning)
        |> mapFields (Map.empty |> Map.add typeName ("Id", "Rev"))

    let toDoc (d: Document) = d.To<'doctype>()

    let toDesignDoc (d: Document): DesignDocument = 
        failwith "not implemented"

    let listOptionsToFs (o: ListOptions) = 
        let keysToOption (k: obj seq) = 
            match List.ofSeq k with 
            | [] -> None 
            | keys -> ListOption.Keys keys |> Some

        let descendingToOption (d: bool option) = 
            match d with 
            | Some true -> Some (ListOption.Direction SortOrder.Descending)
            | Some false -> Some (ListOption.Direction SortOrder.Ascending )
            | _ -> None

        [
            Option.ofNullable o.Limit |> Option.map ListOption.ListLimit
            Option.ofObj o.Key |> Option.map ListOption.Key 
            keysToOption o.Keys
            Option.ofObj o.StartKey |> Option.map ListOption.StartKey
            Option.ofObj o.EndKey |> Option.map ListOption.EndKey 
            Option.ofNullable o.InclusiveEnd |> Option.map ListOption.InclusiveEnd
            Option.ofNullable o.Descending |> descendingToOption
            Option.ofNullable o.Skip |> Option.map ListOption.Skip
            Option.ofNullable o.Group |> Option.map ListOption.Group 
            Option.ofNullable o.GroupLevel |> Option.map ListOption.GroupLevel
            // Do not include the Reduce option in this list, as the view function will figure that out instead.
        ]
        |> List.filter Option.isSome 
        |> List.map Option.get

    let findExpressionToFs (o: FindExpression) = 
        [
            Option.ofObj o.EqualTo |> Option.map FindOperator.EqualTo
            Option.ofObj o.NotEqualTo |> Option.map FindOperator.NotEqualTo
            Option.ofObj o.GreaterThan |> Option.map FindOperator.GreaterThan
            Option.ofObj o.GreaterThanOrEqualTo |> Option.map FindOperator.GreaterThanOrEqualTo
            Option.ofObj o.LesserThan |> Option.map FindOperator.LesserThan 
            Option.ofObj o.LesserThanOrEqualTo |> Option.map FindOperator.LessThanOrEqualTo
        ]
        |> List.filter Option.isSome
        |> List.map Option.get

    let dictToMap fn (dict: Dictionary<'a, 'b>) = 
        dict 
        |> Seq.map (fun kvp -> kvp.Key, fn kvp.Value)
        |> Map.ofSeq

    let task = Async.StartAsTask

    new(couchUrl, databaseName) = Client(Configuration(couchUrl, databaseName))

    member __.GetAsync(id: string, ?rev: string): Task<'doctype> =
        client 
        |> get id rev
        |> Async.Map toDoc
        |> task

    member __.FindByExpressionAsync (exp, ?options): Task<IEnumerable<'doctype>> = 
        failwith "Not implemented"

    member __.FindBySelectorAsync (dict, ?options): Task<IEnumerable<'doctype>> = 
        // TODO: Convert options param to FindOption list
        dict 
        |> dictToMap findExpressionToFs 
        |> find options 
        <| client 
        |> task

    member __.CountAsync(): Task<int> = 
        client 
        |> count 
        |> task

    member __.CountByExpressionAsync (exp): Task<int> = 
        failwith "Not implemented"

    member __.CountBySelectorAsync (dict): Task<int> = 
        dict
        |> dictToMap findExpressionToFs 
        |> countBySelector 
        <| client
        |> task

    member __.ExistsAsync (id, ?rev: string): Task<bool> = 
        client 
        |> exists id rev
        |> task

    member __.ExistsByExpressionAsync (expr): Task<bool> = 
        failwith "Not implemented"

    member __.ExistsBySelectorAsync (dict): Task<bool> = 
        dict 
        |> dictToMap findExpressionToFs
        |> existsBySelector
        <| client
        |> task

    member __.ListWithDocsAsync (?options): Task<ListResponse<'doctype>> =
        failwith "Not implemented"

    member __.ListWithoutDocsAsync (?options): Task<ListResponse<Revision>> = 
        failwith "Not implemented"

    member __.CreateAsync (doc: 'doctype): Task<PostPutCopyResult> = 
        client 
        |> create (Some typeName, doc)
        |> task

    member __.UpdateAsync (id, doc: 'doctype, rev): Task<PostPutCopyResult> = 
        (Some typeName, doc)
        |> update id rev 
        <| client 
        |> task

    member __.CopyAsync (id, newId): Task<PostPutCopyResult> = 
        client 
        |> copy id newId
        |> task
    
    member __.DeleteAsync (id, rev): Task<unit> = 
        client
        |> delete id rev 
        |> task

    /// <summary>
    /// Queries a view. 
    /// NOTE: This function forces the `reduce` parameter to FALSE, i.e. it will NOT reduce. Use the `reduce` functions instead.
    /// </summary>
    member __.ViewAsync<'returnType, 'docType> (designDocName, viewName, ?options): Task<IEnumerable<ViewResponse<'returnType, 'docType>>> = 
        options
        |> Option.map listOptionsToFs
        |> Option.defaultValue []
        |> view designDocName viewName
        <| client
        |> Async.Map (fun result -> result.Rows)
        |> Async.MapSeq (fun row ->
                let vr = ViewResponse<'returnType, 'docType>(row.Key, row.Value.Value.To<'returnType>())
                vr.Doc <- row.Doc |> Option.map (fun d -> d.To<'docType>())
                vr )
        |> task

    /// <summary>
    /// Queries a view and reduces it.
    /// NOTE: This function forces the `reduce` parameter to TRUE< i.e. will ALWAYS reduce. Use the `view` or function to query a view's docs instead.
    /// </summary>
    member __.ReduceAsync<'returnType> (designDocName, viewName, ?options): Task<'returnType> = 
        options 
        |> Option.map listOptionsToFs
        |> Option.defaultValue []
        |> reduce designDocName viewName 
        <| client 
        |> Async.Map (fun d -> d.To<'returnType>())
        |> task

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
    member __.BulkInsert (allowNewEdits: bool, docs: IEnumerable<'doctype>): Task<IEnumerable<BulkResult>> = 
        let mode = 
            match allowNewEdits with 
            | true -> BulkMode.AllowNewEdits
            | false -> BulkMode.NoNewEdits
        
        docs
        |> List.ofSeq
        |> List.map (fun d -> Some typeName, d)
        |> bulkInsert mode
        <| client
        |> Async.Map Seq.ofList
        |> task

    /// <summary>
    /// Creates a CouchDB database if it doesn't exist.
    /// </summary>
    member __.CreateDatabaseAsync(): Task<CreateResult> = 
        client 
        |> createDatabase 
        |> task

    /// <summary>
    /// Deletes the database. This cannot be undone!
    /// </summary>
    member __.DeleteDatabaseAsync(): Task<unit> = 
        client 
        |> deleteDatabase 
        |> task

    /// <summary>
    /// Creates the given design docs. This is a dumb function and will overwrite the data of any design doc that shares its id.
    /// </summary>
    member __.CreateOrUpdateDesignDocAsync (name: string, views: IEnumerable<ViewConfig>): Task<unit> = 
        views 
        |> Seq.fold (fun views view -> views |> Map.add view.Name (view.MapFunction, Option.ofString view.ReduceFunction)) Map.empty
        |> DesignDoc.doc name
        |> createOrUpdateDesignDoc
        <| client 
        |> task

    member __.CreateIndexesAsync (indexes: IEnumerable<string>): Task<IndexInsertResult> = 
        client 
        |> createIndexes (List.ofSeq indexes)
        |> task

    member __.GetCouchVersion(): Task<string> = 
        client 
        |> getCouchVersion 
        |> task 

    member __.IsVersionTwoOrAbove(): Task<bool> = 
        client 
        |> isVersion2OrAbove 
        |> task