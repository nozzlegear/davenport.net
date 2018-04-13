module Tests.Utils

open Extensions
open Expecto
open Expecto.Flip
open Davenport.Utils

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

        testCaseAsync "Removes user id prefix" <| async {
            "org.couchdb.user:hello_world"
            |> removeUserIdPrefix 
            |> Expect.equal "" "hello_world"

            "org.COUCHDB.user:hello_world"
            |> removeUserIdPrefix
            |> Expect.equal "Should ignore case" "hello_world"

            "hello_world_org.couchdb.user:hello_world"
            |> removeUserIdPrefix
            |> Expect.equal "Should only remove from start of string" "hello_world_org.couchdb.user:hello_world"
        }

        testCaseAsync "Formats user ids" <| async {
            "hello_world"
            |> toUserId
            |> Expect.equal "" "org.couchdb.user:hello_world"

            "org.couchdb.user:hello_world"
            |> toUserId 
            |> Expect.equal "Should not have two prefixes" "org.couchdb.user:hello_world"
        }

        testCaseAsync "Formats user database names" <| async {
            // 'hello_world' in hex
            let hex = "userdb-68656c6c6f5f776f726c64"

            "hello_world"
            |> toUserDatabaseName
            |> Expect.equal "" hex

            "org.couchdb.user:hello_world"
            |> toUserDatabaseName 
            |> Expect.equal "Should strip id prefix" hex
        }
    ]
