module Davenport.Fsharp.Types

open Newtonsoft.Json.Linq
open Newtonsoft.Json
open System

type TypeName = string

type DocData = JToken 

type Document = TypeName * DocData

type TotalRows = int

type Offset = int

type DocumentList = TotalRows * Offset * Document list

type Id = string

type Rev = string

type Okay = bool

type SupportedTypeConfig = {
    ``type``: System.Type
    typeName: TypeName
    idField: string option 
    revField: string option
}

type ObjectType = 
    | SystemType of System.Type 
    | StringType of string
    | JtokenOptionType of JToken option

type CouchResult =  TypeName * Newtonsoft.Json.Linq.JObject

type CouchProps = 
    internal { 
        username: string option
        password: string option
        converter: JsonConverter
        databaseName: string
        couchUrl: string
        id: string
        rev: string
        onWarning: Event<string> }

type ViewProps = 
    internal {
        map: string option
        reduce: string option
    }

type internal Method = 
    | Get
    | Post
    | Put
    | Delete
    | Head
    | Copy

type internal RequestProps = {
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
    | Limit of int 
    | Key of obj
    | Keys of obj list
    | StartKey of obj
    | EndKey of obj
    | InclusiveEnd of bool
    | Descending of bool
    | Skip of int

type FindOption = 
    | Fields of string list 
    | Sort of obj list 
    | Limit of int list 
    | Skip of int list
    | UseIndex of obj
    | Selector of string

type IncludeDocs = 
    | WithDocs
    | WithoutDocs

type ViewName = string

type MapFunction = string

type ReduceFunction = string

type View = ViewName * MapFunction * ReduceFunction option

type DesignDoc = Id * View list

type DavenportException (msg, statusCode, statusReason, responseBody, requestUrl) = 
    inherit System.Exception(msg)    
    member __.StatusCode: int = statusCode
    member __.StatusReason: string = statusReason
    member __.ResponseBody: string = responseBody
    member __.RequestUrl: string = requestUrl
    member __.Conflict = statusCode = 409 // 409 Conflict is returned by CouchDB when a conflict occurs