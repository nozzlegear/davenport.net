module Davenport.Fsharp.Tests.Infrastructure

open System
open Expecto
open Expecto.Flip
open Davenport.Infrastructure

[<Tests>]
let tests =
    testList "Davenport.Fsharp.Infrastructure" [
        testCaseAsync "Combines URLs" <| async {
            makeUrl ["http://couchy:5984"; "my_database"; "henlo_world"] (Map.ofSeq ["rev", "abcd"])
            |> Expect.equal "" "http://couchy:5984/my_database/henlo_world?rev=abcd"

            makeUrl ["localhost:5984"; "my_database"; "henlo_world"] (Map.ofSeq ["rev", "abcd"; "foo", "bar"])
            |> Expect.equal "" "http://localhost:5984/my_database/henlo_world?rev=abcd&foo=bar"

            makeUrl ["http://localhost:5984"; "my_database/"; "/henlo_world"] (Map.ofSeq ["rev", "abcd"; "foo", "bar"])
            |> Expect.equal "" "http://localhost:5984/my_database/henlo_world?rev=abcd&foo=bar"

            makeUrl ["LOCALHOST:5984"; "my_database/"; "/henlo_world"] (Map.ofSeq ["rev", "abcd"; "foo", "bar"])
            |> Expect.equal "" "http://LOCALHOST:5984/my_database/henlo_world?rev=abcd&foo=bar"
        }

        testCaseAsync "Detects when a string starts with localhost" <| async {
            match "localhost:5984" with 
            | StartsWithLocalhost _ -> true
            | _ -> false
            |> Expect.isTrue ""
            
            match "LOCALHOST:5984" with
            | StartsWithLocalhost _ -> true
            | _ -> false
            |> Expect.isTrue ""

            match "http://localhost:5984" with 
            | StartsWithLocalhost _ -> true
            | _ -> false
            |> Expect.isFalse ""
        }
    ]
