module Davenport.Utils 

open System
open Types
        
let (|NotNullOrEmpty|_|) (s: string) = 
    match String.IsNullOrEmpty s with 
    | true -> None
    | false -> Some s

let (|StartsWithProtocol|_|) (s: string) = 
    [
        Uri.UriSchemeHttp
        Uri.UriSchemeHttps
    ]
    |> Seq.fold (fun state value -> 
        match state, value with 
        | Some x, _ -> Some x
        | None, scheme when s.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) -> Some s
        | None, _ -> None) None

module internal Async = 
    let Map (fn: 't -> 'u) task = async {
        let! result = task

        return fn result
    }

    let MapSeq (fn: 't -> 'u) task = async {
        let! result = task

        return Seq.map fn result
    }


module internal String = 
    open System.Text
    let Lowercase (s: string) = s.ToLower()
    let Uppercase (s: string) = s.ToUpper()
    let EqualsIgnoreCase (s1: string) (s2: string) = String.Equals(s1, s2, StringComparison.OrdinalIgnoreCase)
    let ToHex (input: string) =
        input
        |> Seq.map System.Convert.ToInt32
        |> Seq.fold (fun (output: StringBuilder) ch -> output.Append(ch.ToString("x"))) (StringBuilder())
        |> fun sb -> sb.ToString()

module internal List = 
    let ofSingle x = [x]
    let appendSingle x list = list@[x]

module internal Option = 
    let iterSeq fn list = list |> Seq.iter (Option.iter fn)
    let defaultBindWith fn opt = 
        match opt with 
        | Some x -> Some x
        | None -> fn()
    /// <summary>
    /// The same as Option.iter, but returns the option so it can be used further.
    /// </summary>
    let iter2 fn opt = 
        opt 
        |> Option.iter fn 

        opt
    let ofString s = 
        match s with 
        | NotNullOrEmpty s -> Some s
        | _ -> None
    
    /// <summary>
    /// Converts a sequence to an option, returning `Some sequence` if the length is greater than 0, else `None`.
    /// </summary>
    let ofSeq s = 
        if Seq.length s > 0 then Some s else None

module internal Int = 
    let parse s = 
        try 
            System.Int32.Parse s |> Some
        with 
        | _ -> None

module internal Long = 
    let parse s = 
        try 
            System.Int64.Parse s |> Some 
        with 
        | _ -> None

let rec findDavenportExceptionOrRaise (exn: Exception) = 
    match exn with 
    | :? System.AggregateException as exn -> findDavenportExceptionOrRaise exn.InnerException 
    | :? DavenportException as exn -> exn 
    | _ -> raise exn
    
let makeUrl pathSegments (querystring: Map<string, string>) = 
    let rec combinePaths (remaining: string list) output = 
        match remaining with 
        | segment::rest -> combinePaths rest (output@[segment.Trim '/'])
        | [] -> output

    let ub = 
        match String.Join("/", combinePaths pathSegments []) with 
        | StartsWithProtocol s -> s
        | s -> sprintf "%s%s%s" Uri.UriSchemeHttp Uri.SchemeDelimiter s
        |> System.UriBuilder
        
    ub.Query <- 
        querystring 
        |> Seq.map (fun kvp -> sprintf "%s=%s" kvp.Key (System.Net.WebUtility.UrlEncode kvp.Value))
        |> fun s -> String.Join("&", s)

    ub.ToString()

open System.Text.RegularExpressions

/// <summary>
/// Removes the `org.couchdb.user:` prefix from a user id string.
/// </summary>
let removeUserIdPrefix userId = 
    let rgx = Regex("^org\.couchdb\.user:", RegexOptions.IgnoreCase)
    rgx.Replace(userId, "")

/// <summary>
/// Takes a user id and returns their database name when CouchDB is set to `couch_peruser` mode. It does this by removing the `org.couchdb.user:` prefix (if applicable), converting the rest to hex, and prefixing with `userdb-`. This is the same scheme that CouchDB itself uses when creating user-specific databases in `couch_peruser` mode.
/// NOTE: this is case sensitive!
/// </summary>
let toUserDatabaseName userId = 
    userId 
    |> removeUserIdPrefix 
    |> String.ToHex 
    |> sprintf "userdb-%s"

/// <summary>
/// Takes a username and formats it to CouchDB's `org.couchdb.user:username` scheme.
/// </summary>
let toUserId userName = 
    userName 
    |> removeUserIdPrefix 
    |> sprintf "org.couchdb.user:%s"
    |> String.Lowercase