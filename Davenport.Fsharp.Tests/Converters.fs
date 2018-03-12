module Davenport.Fsharp.Tests.Conveters

open System
open Expecto
open Expecto.Flip
open Davenport.Converters
open Davenport.Types
open Newtonsoft.Json

type MyDoc = {
    Id: string
    Rev: string
    Foo: bool
    Bar: int
    Hello: string
}

[<Tests>]
let tests =
    let converter = DefaultConverter (Map.ofSeq ["my-type", ("Id", "Rev")])
    let toJson data = JsonConvert.SerializeObject(data, [|converter :> JsonConverter|])

    testList "Davenport.Fsharp.Converters" [
        testCaseAsync "Serializes an InsertedDocument" <| async {
            let doc = {
                Id = "my-doc-id"
                Rev = "my-doc-rev"
                Foo = true
                Bar = 17
                Hello = "world"
            }
            let inserted: InsertedDocument<MyDoc> = Some "my-type", doc
            
            toJson inserted
            |> Expect.equal "Should serialize to expected string" "{}"

            ()
        }
    ]
