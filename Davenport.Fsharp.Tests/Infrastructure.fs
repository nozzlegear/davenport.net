module Davenport.Fsharp.Tests.Infrastructure

open System
open Expecto
open Davenport.Fsharp.Infrastructure

[<Tests>]
let tests =
    testList "Davenport.Fsharp.Infrastructure" [
        testCaseAsync "Combines URLs" <| async {
            makeUrl ["http://couchy:5984"; "my_database"; "henlo_world"] [QS ("rev", "abcd")]
            |> fun s -> Expect.equal s "http://couchy:5984/my_database/henlo_world?rev=abcd" ""

            makeUrl ["localhost:5984"; "my_database"; "henlo_world"] [QS ("rev", "abcd"); QS ("foo", "bar")]
            |> fun s -> Expect.equal s "http://localhost:5984/my_database/henlo_world?rev=abcd&foo=bar" ""

            makeUrl ["http://localhost:5984"; "my_database/"; "/henlo_world"] [QS ("rev", "abcd"); QS ("foo", "bar")]
            |> fun s -> Expect.equal s "http://localhost:5984/my_database/henlo_world?rev=abcd&foo=bar" ""

            makeUrl ["LOCALHOST:5984"; "my_database/"; "/henlo_world"] [QS ("rev", "abcd"); QS ("foo", "bar")]
            |> fun s -> Expect.equal s "http://LOCALHOST:5984/my_database/henlo_world?rev=abcd&foo=bar" ""
        }

        testCaseAsync "Detects when a string starts with localhost" <| async {
            match "localhost:5984" with 
            | StartsWithLocalhost _ -> true
            | _ -> false
            |> fun x -> Expect.isTrue x ""

            match "LOCALHOST:5984" with
            | StartsWithLocalhost _ -> true
            | _ -> false
            |> fun x -> Expect.isTrue x ""

            match "http://localhost:5984" with 
            | StartsWithLocalhost _ -> true
            | _ -> false
            |> fun x -> Expect.isFalse x ""
        }
    ]
