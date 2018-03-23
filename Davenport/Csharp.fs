module Davenport.Csharp

open Davenport.Fsharp 
open Davenport.Types 
open Davenport.Infrastructure
open Davenport.Converters
open System
open System.Threading.Tasks
open Newtonsoft.Json
open System.Collections.Generic

[<AbstractClass>]
type CouchDoc() = 
    abstract member Id: string with get, set
    abstract member Rev: string with get, set

type Configuration(couchUrl: string, databaseName: string) = 
    member val CouchUrl = couchUrl with get, set
    member val DatabaseName = databaseName with get, set
    member val Username = "" with get, set
    member val Password = "" with get, set
    member val JsonConverter: JsonConverter = null with get, set
    member val Warning: EventHandler<string> = null with get, set

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

    let task = Async.StartAsTask

    new(couchUrl, databaseName) = Client(Configuration(couchUrl, databaseName))

    member __.GetAsync(id: string, ?rev: string): Task<'doctype> =
        client 
        |> get id rev
        |> Async.Map toDoc
        |> task

    member __.FindByExpressionAsync (exp, ?options): Task<IEnumerable<'doctype>> = 
        failwith "Not implemented"

    member __.FindByObjectAsync (obj, ?options): Task<IEnumerable<'doctype>> =
        failwith "Not implemented"

    member __.FindBySelectorAsync (dict, ?options): Task<IEnumerable<'doctype>> = 
        failwith "Not implemented"

    member __.CountAsync(): Task<int> = 
        client 
        |> count 
        |> task

    member __.CountByExpressionAsync (exp): Task<int> = 
        failwith "Not implemented"

    member __.CountByObjectAsync (obj): Task<int> =
        failwith "Not implemented"

    member __.CountBySelectorAsync (dict): Task<int> = 
        failwith "Not implemented"

    member __.ExistsAsync (id, ?rev: string): Task<bool> = 
        client 
        |> exists id rev
        |> task

    member __.ExistsByExpressionAsync (expr): Task<bool> = 
        failwith "Not implemented"

    member __.ExistsByObjectAsync (obj): Task<bool> = 
        failwith "Not implemented"

    member __.ExistsBySelectorAsync (dict): Task<bool> = 
        failwith "Not implemented"

    member __.ListWithDocsAsync (?options): Task<ListResponse<DocumentType>> =
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

    member __.ViewAsync<'returnType> (designDocName, viewName, ?options): Task<IEnumerable<ViewResult<'returnType>>> = 
        // TODO: Determine if we should use the `view` or `reduce` functions based on the options passed in.
        failwith "Not implemented"

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

    member __.CreateDatabaseAsync(): Task<CreateResult> = 
        client 
        |> createDatabase 
        |> task

    member __.DeleteDatabaseAsync(): Task<unit> = 
        client 
        |> deleteDatabase 
        |> task

    member __.CreateOrUpdateDesignDocAsync (doc): Task<IEnumerable<PostPutCopyResult>> = 
        failwith "Not implemented"

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