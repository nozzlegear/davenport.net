module Davenport.Types 

open Newtonsoft.Json.Linq
open Newtonsoft.Json

type TypeName = string

type IdFieldName = string

type RevFieldName = string

type FieldMapping = Map<TypeName, IdFieldName * RevFieldName>

type TotalRows = int

type Offset = int

type Warning = string

type Id = string

type Rev = string

type Okay = bool

type DocData = JToken 

type ViewValue = JToken

type Document = TypeName option * DocData

type FindResult = Warning option * Document list

type SerializableData = obj

type InsertedDocument = TypeName option * SerializableData

type PostPutCopyResult = Id * Rev * Okay

type Serializable = 
    | Serializable of InsertedDocument

type ViewKey = 
    | Key of obj
    | KeyList of obj list

type ViewDoc = Id * ViewKey * ViewValue * Document option

type ViewResult = TotalRows * Offset * ViewDoc list

type CouchResult =  TypeName option * Newtonsoft.Json.Linq.JObject

type BulkErrorType = 
    | Conflict
    | Forbidden
    | Unauthorized
    | Other of string

type BulkErrorReason = string

type BulkDocumentError = Id * BulkErrorType * BulkErrorReason

type BulkResult = 
    | Inserted of PostPutCopyResult
    | Failed of BulkDocumentError

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

type CreateDatabaseResult = 
    | Created
    | AlreadyExisted

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

type Views = Map<ViewName, MapFunction * ReduceFunction option>

type DesignDoc = Id * Views

[<AbstractClass>]
type ICouchConverter() = 
    abstract AddFieldMappings: FieldMapping -> unit
    abstract GetFieldMappings: unit -> FieldMapping
    abstract ConvertListOptionsToMap: ListOption -> string
    abstract ConvertFindOptionsToMap: FindOption -> string
    abstract ConvertRevToMap: Rev -> string
    abstract WriteInsertedDocument: FieldMapping -> JsonWriter -> InsertedDocument
    abstract ReadToDocument: JsonReader -> Document
    abstract ReadToViewResult: JsonReader -> ViewResult 
    abstract ReadToPostPutCopyResponse: JsonReader -> PostPutCopyResult
    abstract ReadToFindResult: JsonReader -> FindResult 
    abstract ReadToBulkResultList: JsonReader -> BulkResult list
    abstract ReadJToken: JsonReader -> JToken

type CouchProps = 
    internal { 
        username: string option
        password: string option
        converter: ICouchConverter
        databaseName: string
        couchUrl: string
        onWarning: Event<string> }
        
type RequestProps = {
    querystring: Map<string, string> option
    headers: Map<string, string> option
    body: string option
    couchProps: CouchProps
    path: string
}    

type DavenportException (msg, statusCode, statusReason, responseBody, requestUrl) = 
    inherit System.Exception(msg)    
    member __.StatusCode: int = statusCode
    member __.StatusReason: string = statusReason
    member __.ResponseBody: string = responseBody
    member __.RequestUrl: string = requestUrl
    member __.Conflict = statusCode = 409 // 409 Conflict is returned by CouchDB when a conflict occurs