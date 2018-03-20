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

type Document(t, r, s) =
    member __.TypeName: string option = t
    member __.Raw: JToken = r
    /// <summary>
    /// The suggested JsonSerializer for deserializing the Raw document object.
    /// </summary>
    member __.Serializer: JsonSerializer = s
    /// <summary>
    /// Converts the Raw document object to the given type using the document's serializer.
    /// </summary>
    member x.ToObject<'a> (): 'a = x.Raw.ToObject<'a>(x.Serializer)
    /// <summary>
    /// An alias for the ToObject<'a> method.
    /// </summary>
    member x.To<'a> (): 'a = x.ToObject<'a>()

type InsertedDocument<'a> = TypeName option * 'a

type PostPutCopyResult = 
    { Id: Id
      Rev: Rev
      Okay: Okay }

type ViewValue = Document

type KeyValue = 
    | String of string 
    | Int of int 
    | Long of int64
    | Float of float 
    | Date of System.DateTime
    | Bool of bool 
    | List of KeyValue list
    | Null 
    | JToken of JToken

type ViewKey = 
    | Key of KeyValue
    | KeyList of KeyValue list    

type ViewDoc = 
    { Id: Id
      Key: ViewKey
      Value: ViewValue option
      Doc: Document option }

type ViewResult = 
    { TotalRows: TotalRows
      Offset: Offset
      Rows: ViewDoc list }

type BulkErrorType = 
    | Conflict
    | Forbidden
    | Unauthorized
    | Other of string

type BulkErrorReason = string

type BulkDocumentError = 
    { Id: Id
      ErrorType: BulkErrorType
      ErrorReason: BulkErrorReason }

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

type FindOperator =
    | EqualTo of obj
    | NotEqualTo of obj
    | GreaterThan of obj
    | LesserThan of obj
    | GreaterThanOrEqualTo of obj
    | LessThanOrEqualTo of obj

type FindOption = 
    | Fields of string list 
    | SortBy of Sort list 
    | FindLimit of int
    | Skip of int
    | UseIndex of obj

type FindResult = Warning option * Document list

type FindSelector = Map<string, FindOperator list>        

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

type IndexName = string 

type IndexField = string

type JsonKey = string

type JsonValue = 
    | StringProp of JsonKey * string 
    | IntProp of JsonKey * int 
    | BoolProp of JsonKey * bool
    | ObjectProp of JsonKey * JsonValue list
    | ArrayProp of JsonKey * JsonValue list
    | RawProp of JsonKey * string
    | JProp of JProperty
    | String of string 
    | Int of int 
    | Bool of bool
    | Object of JsonValue list 
    | Array of JsonValue list
    | Raw of string

type JsonParseKind = 
    | JsonString of string 
    | JsonObject of JObject 
    | JsonToken of JToken    

[<AbstractClass>]
type ICouchConverter() = 
    abstract ConvertListOptionsToMap: ListOption list -> Map<string, string>
    abstract ConvertRevToMap: Rev -> Map<string, string>
    abstract WriteBulkInsertList: FieldMapping -> BulkMode -> InsertedDocument<'a> list -> string
    abstract WriteInsertedDocument: FieldMapping -> InsertedDocument<'a> -> string
    abstract WriteDesignDoc: Views -> string
    abstract WriteIndexes: IndexName -> IndexField list -> string
    abstract WriteFindSelector: FindOption list -> FindSelector -> string
    abstract ReadAsDocument: FieldMapping -> string -> Document
    abstract ReadAsViewResult: FieldMapping -> string -> ViewResult 
    abstract ReadAsPostPutCopyResponse: string -> PostPutCopyResult
    abstract ReadAsFindResult: FieldMapping -> string -> FindResult 
    abstract ReadAsBulkResultList: string -> BulkResult list
    abstract ReadVersionToken: string -> string 
    abstract ReadAsJToken: FieldMapping -> JsonParseKind -> (TypeName option * JToken)

type CouchProps = 
    internal { 
        username: string option
        password: string option
        converter: ICouchConverter
        databaseName: string
        couchUrl: string
        onWarning: Event<string>
        fieldMapping: FieldMapping }
        
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