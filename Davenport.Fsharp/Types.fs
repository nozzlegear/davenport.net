module Davenport.Fsharp.Types

open Newtonsoft.Json.Linq
open Newtonsoft.Json
open System

type TypeName = string

type DocData = JToken 

type Document = TypeName * DocData

type TotalRows = int

type Offset = int

type DocumentList = TotalRows -> Offset -> Document list

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
        onWarning: (IEvent<EventHandler<string>,string> -> unit) option }

type ViewProps = 
    internal {
        map: string option
        reduce: string option
    }

/// Translates the C# PostPutCopyResponse, which contains a nullable bool Ok prop, to F# removing the nullable bool.
type FsPostPutCopyResponse = {
    Id: string
    Rev: string
    Ok: bool
}

type Data = 
    | JsonString of string 
    | JsonObject of obj

type Find =
    | EqualTo of obj
    | NotEqualTo of obj
    | GreaterThan of obj
    | LesserThan of obj
    | GreaterThanOrEqualTo of obj
    | LessThanOrEqualTo of obj

type DavenportException (msg, statusCode, statusReason, responseBody, requestUrl) = 
    inherit System.Exception(msg)    
    member __.StatusCode: int = statusCode
    member __.StatusReason: string = statusReason
    member __.ResponseBody: string = responseBody
    member __.RequestUrl: string = requestUrl
    member __.Conflict = statusCode = 409 // 409 Conflict is returned by CouchDB when a conflict occurs