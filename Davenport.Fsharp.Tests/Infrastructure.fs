module Tests.Infrastructure

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
            |> Expect.equal "" "http://localhost:5984/my_database/henlo_world?foo=bar&rev=abcd"

            makeUrl ["http://localhost:5984"; "my_database/"; "/henlo_world"] (Map.ofSeq ["rev", "abcd"; "foo", "bar"])
            |> Expect.equal "" "http://localhost:5984/my_database/henlo_world?foo=bar&rev=abcd"

            makeUrl ["LOCALHOST:5984"; "my_database/"; "/henlo_world"] (Map.ofSeq ["rev", "abcd"; "foo", "bar"])
            |> Expect.equal "" "http://localhost:5984/my_database/henlo_world?foo=bar&rev=abcd"

            makeUrl ["username:password@localhost:5984"; "my_database"] (Map.ofSeq ["rev", "abcd"])
            |> Expect.equal "username:password failed" "http://username:password@localhost:5984/my_database?rev=abcd"
        }

        testCaseAsync "Detects when a string starts with a protocol" <| async {
            match "localhost:5984" with 
            | StartsWithProtocol _ -> true
            | _ -> false
            |> Expect.isFalse "First failed"
            
            match "LOCALHOST:5984" with
            | StartsWithProtocol _ -> true
            | _ -> false
            |> Expect.isFalse "Second failed"

            match "http://localhost:5984" with 
            | StartsWithProtocol _ -> true
            | _ -> false
            |> Expect.isTrue "Third failed"

            match "username:password@localhost:5984" with 
            | StartsWithProtocol _ -> true
            | _ -> false 
            |> Expect.isFalse "Fourth failed"

            match "http://username:password@localhost:5984" with 
            | StartsWithProtocol _ -> true 
            | _ -> false
            |> Expect.isTrue "Fifth failed"
        }
    ]
