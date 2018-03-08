module Davenport.Fsharp.Infrastructure

open System
open System.Net
open System.Net.Http
open Newtonsoft.Json
open System.Net.Http.Headers
open Davenport.Entities
open Newtonsoft.Json
open System
open System.Linq.Expressions
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Linq.RuntimeHelpers
open System.Net.Http
open System.Net.Http.Headers
open System.Net
open Davenport.Fsharp.Converters
open Davenport.Infrastructure

type CouchProps = 
    private { 
        username: string option
        password: string option
        converter: JsonConverter
        databaseName: string
        couchUrl: string
        id: string
        rev: string
        onWarning: (IEvent<EventHandler<string>,string> -> unit) option }
    with 
    static member internal Default = 
        { username = None 
          password = None 
          converter = FsConverter("id", "rev", None)
          databaseName = ""
          couchUrl = ""
          id = "_id"
          rev = "_rev" 
          onWarning = None }

type ViewProps = 
    private {
        map: string option
        reduce: string option
    }
    with
    static member internal Default = { map = None; reduce = None }

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

/// <summary>
/// Converts an F# expression to a LINQ expression, then converts that LINQ expression to a Map<string, Find> due to an incompatibility with the FsDoc and the types expected by Find, Exists and CountByExpression functions.
/// </summary>
let private convertExprToMap<'a> (expr : Expr<'a -> bool>) =
    /// Source: https://stackoverflow.com/a/23390583
    let linq = LeafExpressionConverter.QuotationToExpression expr
    let call = linq :?> MethodCallExpression
    let lambda = call.Arguments.[0] :?> LambdaExpression
    
    Expression.Lambda<Func<'a, bool>>(lambda.Body, lambda.Parameters)
    |> Davenport.Infrastructure.ExpressionParser.Parse

let convertMapToDict (map: Map<string, Find list>) =
    let rec convert remaining (expression: FindExpression) =
        match remaining with
        | EqualTo x::tail ->
            expression.EqualTo <- x
            convert tail expression
        | NotEqualTo x::tail ->
            expression.NotEqualTo <- x
            convert tail expression
        | GreaterThan x::tail ->
            expression.GreaterThan <- x
            convert tail expression
        | LesserThan x::tail ->
            expression.LesserThan <- x
            convert tail expression
        | GreaterThanOrEqualTo x::tail ->
            expression.GreaterThanOrEqualTo <- x
            convert tail expression
        | LessThanOrEqualTo x::tail ->
            expression.LesserThanOrEqualTo <- x
            convert tail expression
        | [] -> expression

    map
    |> Map.map (fun _ list -> convert list (FindExpression()))
    |> Collections.Generic.Dictionary

let convertPostPutCopyResponse (r: PostPutCopyResponse) =
    // Convert 'Ok' prop to false if it's null.
    { Id = r.Id
      Rev = r.Rev
      Ok = Option.ofNullable r.Ok |> Option.defaultValue false }

let rec private findDavenportExceptionOrRaise (exn: Exception) = 
    match exn with 
    | :? System.AggregateException as exn -> findDavenportExceptionOrRaise exn.InnerException 
    | :? Davenport.Infrastructure.DavenportException as exn -> exn 
    | _ -> raise exn

let (|StartsWithLocalhost|_|) (s: string) = 
    match s.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) with
    | true -> Some s
    | false ->None

let makeUrl pathSegments (querystring: Map<string, string>) = 
    let rec combinePaths (remaining: string list) output = 
        match remaining with 
        | segment::rest -> combinePaths rest (output@[segment.Trim '/'])
        | [] -> output

    let ub = 
        match String.Join("/", combinePaths pathSegments []) with 
        | StartsWithLocalhost s -> sprintf "%s%s%s" Uri.UriSchemeHttp Uri.SchemeDelimiter s
        | s -> s
        |> System.UriBuilder
        
    ub.Query <- 
        querystring 
        |> Seq.map (fun kvp -> sprintf "%s=%s" kvp.Key (System.Net.WebUtility.UrlEncode kvp.Value))
        |> fun s -> String.Join("&", s)

    ub.ToString()
    
let private httpClient = new HttpClient()

let toJson converter object = JsonConvert.SerializeObject(object, [|converter|])

let ofJson<'a> converter json = JsonConvert.DeserializeObject<'a>(json, [|converter|])

let revMap (rev: string option) =
    rev
    |> Option.map (fun rev -> ["rev", rev :> obj])
    |> Option.defaultValue []
    |> Map.ofSeq

let executeRequest method path (qs: Map<string, obj>) (body: 'a option) (props: CouchProps) = 
    // JsonSerialize any query string value.
    let url = 
        qs
        |> Map.map (fun _ value -> toJson props.converter value)
        |> makeUrl [props.couchUrl; props.databaseName; path]
    let req = new HttpRequestMessage(method, url)
    
    match props.username, props.password with 
    | None, None -> ()
    | _, _ -> 
        let username = Option.defaultValue "" props.username
        let password = Option.defaultValue "" props.password
        let combined = 
            sprintf "%s:%s" username password 
            |> System.Text.Encoding.UTF8.GetBytes 
            |> System.Convert.ToBase64String

        req.Headers.Authorization <- AuthenticationHeaderValue("Basic", combined)
        
    match body with 
    | None -> ()
    | Some body -> 
        let message = 
            toJson props.converter body
            |> System.Text.Encoding.UTF8.GetBytes
            |> fun b -> new ByteArrayContent(b)

        message.Headers.ContentType <- MediaTypeHeaderValue "application/json"
        req.Content <- message

    async {
        let! response = 
            httpClient.SendAsync req
            |> Async.AwaitTask
        let! rawBody = 
            response.Content.ReadAsStringAsync()
            |> Async.AwaitTask

        if not response.IsSuccessStatusCode 
        then 
            let code = int response.StatusCode
            let message = 
                sprintf "Error with %s request for CouchDB database %s at %s. %i %s"
                    method.Method props.databaseName url code response.ReasonPhrase

            let ex = DavenportException(message)
            ex.StatusCode <- code
            ex.StatusText <- response.ReasonPhrase
            ex.ResponseBody <- rawBody
            ex.Url <- url

            raise ex

        return rawBody
    }

let mapDoc () = ""

let mapDocSync () = ""