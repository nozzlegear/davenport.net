module Davenport.Infrastructure

open System.Net.Http
open System.Net.Http.Headers
open Types
open Utils

// /// <summary>
// /// Converts an F# expression to a LINQ expression, then converts that LINQ expression to a Map<string, Find> due to an incompatibility with the FsDoc and the types expected by Find, Exists and CountByExpression functions.
// /// </summary>
// let convertExprToMap<'a> (expr : Expr<'a -> bool>) =
//     /// Source: https://stackoverflow.com/a/23390583
//     let linq = LeafExpressionConverter.QuotationToExpression expr
//     let call = linq :?> MethodCallExpression
//     let lambda = call.Arguments.[0] :?> LambdaExpression
    
//     Expression.Lambda<Func<'a, bool>>(lambda.Body, lambda.Parameters)
//     |> Davenport.Infrastructure.ExpressionParser.Parse

// Because of the way http connections work, it's best to have one HttpClient instance for the whole application.
// https://blogs.msdn.microsoft.com/alazarev/2017/12/29/disposable-finalizers-and-httpclient/
let private httpClient = new HttpClient()

let request path props = { 
    querystring = None
    headers = None 
    body = None
    couchProps = props
    path = path
}

let querystring qs props = { props with querystring = Some qs }

let headers headers props = { props with headers = Some headers }

let body body props = { props with body = Some body }

let send (method: Method) (request: RequestProps) = 
    let props = request.couchProps

    let url = 
        request.querystring
        |> Option.defaultValue Map.empty
        |> makeUrl [props.couchUrl; props.databaseName; request.path]

    let method = 
        match method with 
        | Get -> HttpMethod.Get
        | Post -> HttpMethod.Post
        | Put -> HttpMethod.Put
        | Delete -> HttpMethod.Delete
        | Head -> HttpMethod.Head
        | Copy -> HttpMethod "COPY"

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
        
    match request.body with 
    | None -> ()
    | Some body -> 
        let message = 
            System.Text.Encoding.UTF8.GetBytes body
            |> fun b -> new ByteArrayContent(b)

        message.Headers.ContentType <- MediaTypeHeaderValue "application/json"
        req.Content <- message

    match request.headers with 
    | None -> ()
    | Some headers -> Map.iter(fun key value -> req.Headers.Add(key, [value])) headers

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

            DavenportException(message, code, response.ReasonPhrase, rawBody, url)
            |> raise

        return rawBody
    }