module Davenport.Types 

open Newtonsoft.Json.Linq
open Newtonsoft.Json

type TypeName = string

type IdFieldName = string

type RevFieldName = string

type FieldMapping = Map<TypeName, IdFieldName * RevFieldName>

[<AbstractClass>]
type ICouchConverter() = 
    inherit JsonConverter()
    abstract AddFieldMappings: FieldMapping -> unit
    abstract GetFieldMappings: unit -> FieldMapping

type TotalRows = int

type Offset = int

type Warning = string

type Id = string

type Rev = string

type Okay = bool

type DocData = JToken 

type Document = TypeName option * DocData

type DocumentList = TotalRows * Offset * Document list

type FoundList = Warning option * Document list

type SerializableData<'a> = 'a

type InsertedDocument<'a> = TypeName option * SerializableData<'a>

type ViewKey = 
    | Key of obj
    | KeyList of obj list

type ViewDoc = ViewKey * Document

type CouchResult =  TypeName option * Newtonsoft.Json.Linq.JObject

type CouchProps = 
    internal { 
        username: string option
        password: string option
        converter: ICouchConverter
        databaseName: string
        couchUrl: string
        onWarning: Event<string> }

type ViewProps = 
    internal {
        map: string option
        reduce: string option
    }

type Method = 
    | Get
    | Post
    | Put
    | Delete
    | Head
    | Copy

type RequestProps = {
    querystring: Map<string, obj> option
    headers: Map<string, string> option
    body: obj option
    couchProps: CouchProps
    path: string
}

type CreateDatabaseResult = 
    | Created
    | AlreadyExisted

type PostPutCopyResponse = Id * Rev * Okay

type Find =
    | EqualTo of obj
    | NotEqualTo of obj
    | GreaterThan of obj
    | LesserThan of obj
    | GreaterThanOrEqualTo of obj
    | LessThanOrEqualTo of obj

type ListOption = 
    | ListLimit of int 
    | Key of obj
    | Keys of obj list
    | StartKey of obj
    | EndKey of obj
    | InclusiveEnd of bool
    | Descending of bool
    | Skip of int
    | Reduce of bool
    | Group of bool
    | GroupLevel of int

type FindOption = 
    | Fields of string list 
    | Sort of obj list 
    | FindLimit of int list 
    | Skip of int list
    | UseIndex of obj
    | Selector of string



type IncludeDocs = 
    | WithDocs
    | WithoutDocs

type BulkMode = 
    | AllowNewEdits
    | NoNewEdits

type ViewName = string

type MapFunction = string

type ReduceFunction = string

type View = ViewName * MapFunction * ReduceFunction option

type DesignDoc = Id * View list

type BulkErrorType = 
    | Conflict
    | Forbidden
    | Unauthorized
    | Other of string

type BulkErrorReason = string

type BulkDocumentError = Id * BulkErrorType * BulkErrorReason

type BulkResponse = 
    | Inserted of PostPutCopyResponse
    | Failed of BulkDocumentError

type DavenportException (msg, statusCode, statusReason, responseBody, requestUrl) = 
    inherit System.Exception(msg)    
    member __.StatusCode: int = statusCode
    member __.StatusReason: string = statusReason
    member __.ResponseBody: string = responseBody
    member __.RequestUrl: string = requestUrl
    member __.Conflict = statusCode = 409 // 409 Conflict is returned by CouchDB when a conflict occurs