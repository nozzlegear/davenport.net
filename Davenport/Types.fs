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

type ViewKey = 
    | String of string 
    | Int of int 
    | Long of int64
    | Float of float 
    | Date of System.DateTime
    | Bool of bool 
    | List of ViewKey list
    | Null 
    | JToken of JToken

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
    | Other of string

type BulkErrorReason = string

type BulkError = 
    { Id: Id
      Error: BulkErrorType
      Reason: BulkErrorReason
      Rev: Rev option }

type BulkResult = 
    | Inserted of PostPutCopyResult
    | Failed of BulkError

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

type CreateResult = 
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
    | IncludeDocs of bool

type IndexType = 
    | Json

type IndexOption = 
    | DDoc of string     
    | Name of string 
    | Type of IndexType 

type IndexName = string 

type IndexField = string

type IndexInsertResult = {
    Result: CreateResult 
    Id: Id
    Name: IndexName
}

type SortFieldName = string

type Sort = | Sort of SortFieldName * SortOrder

type FindOperator =
    | EqualTo of obj
    | NotEqualTo of obj
    | GreaterThan of obj
    | LesserThan of obj
    | GreaterThanOrEqualTo of obj
    | LessThanOrEqualTo of obj

type UseIndex = 
    | FromDesignDoc of Id
    | FromDesignDocAndIndex of Id * IndexName

type FindOption = 
    | Fields of string list 
    | SortBy of Sort list 
    | FindLimit of int
    | Skip of int
    /// <summary>
    /// Optional: Instructs the query to use a specific index.
    /// </summary>
    | UseIndex of UseIndex

type FindResult = Warning option * Document list

type FindSelector = Map<string, FindOperator list>        

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
    abstract WriteIndexes: IndexOption list -> IndexField list -> string
    abstract WriteFindSelector: FindOption list -> FindSelector -> string
    abstract ReadAsDocument: FieldMapping -> JsonParseKind -> Document
    abstract ReadAsViewResult: FieldMapping -> JsonParseKind -> ViewResult 
    abstract ReadAsPostPutCopyResponse: JsonParseKind -> PostPutCopyResult
    abstract ReadAsFindResult: FieldMapping -> JsonParseKind -> FindResult 
    abstract ReadAsBulkResultList: JsonParseKind -> BulkResult list
    abstract ReadVersionToken: JsonParseKind -> string 
    abstract ReadAsJToken: FieldMapping -> JsonParseKind -> (TypeName option * JToken)
    abstract ReadAsIndexInsertResult: JsonParseKind -> IndexInsertResult

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