module Davenport.Types 

open Newtonsoft.Json.Linq
open Newtonsoft.Json
open System.Text

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

type SortOrder = 
    | Ascending
    | Descending 

type ListOption = 
    | ListLimit of int 
    | Key of obj
    | Keys of obj list
    | StartKey of obj
    | EndKey of obj
    | InclusiveEnd of bool
    | Direction of SortOrder
    | Skip of int
    | Reduce of bool
    | Group of bool
    | GroupLevel of int

type SortFieldName = string

type Sort = | Sort of SortFieldName * SortOrder

type FindOption = 
    | Fields of string list 
    | SortBy of Sort list 
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

type JsonKey = string

type JsonValue = 
    | StringProp of JsonKey * string 
    | IntProp of JsonKey * int 
    | ObjectProp of JsonKey * JsonValue
    | ArrayProp of JsonKey * JsonValue list
    | RawProp of JsonKey * string
    | JProp of JProperty
    | String of string 
    | Int of int 
    | Object of JsonValue 
    | Array of JsonValue list
    | Raw of string

[<AbstractClass>]
type ICouchConverter() = 
    abstract ConvertListOptionsToMap: ListOption list -> Map<string, string>
    abstract ConvertFindOptionsToMap: FindOption list -> Map<string, string>
    abstract ConvertRevToMap: Rev -> Map<string, string>
    abstract WriteInsertedDocument: FieldMapping -> InsertedDocument -> string
    abstract WriteUnknownObject: FieldMapping -> 'a -> string
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