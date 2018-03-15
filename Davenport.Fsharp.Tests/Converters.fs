module Tests.Conveters

open System
open Expecto
open Expecto.Flip
open Davenport.Converters
open Davenport.Types
open Newtonsoft.Json

type MyUnion = 
    | Ascended of int 
    | Descended of int 
    | SomethingElse

type MyDoc = {
    Id: string
    Rev: string
    Foo: bool
    Bar: int
    Hello: string
    Token: string option 
    OtherToken: string option 
    Thing: MyUnion
    OtherThing: MyUnion
}

[<Tests>]
let tests =
    let converter = DefaultConverter ()
    let mapping: FieldMapping = Map.ofSeq ["my-type", ("Id", "Rev")]

    ftestList "Davenport.Fsharp.Converters" [
        testCaseAsync "Serializes an InsertedDocument" <| async {
            let doc = {
                Id = "my-doc-id"
                Rev = "my-doc-rev"
                Foo = true
                Bar = 17
                Hello = "world"
                Token = Some "test token"
                OtherToken = None
                Thing = Descended 15
                OtherThing = SomethingElse
            }
            let inserted: InsertedDocument<_> = Some "my-type", doc
            
            inserted            
            |> converter.WriteInsertedDocument mapping
            |> Expect.equal "Should serialize to expected string" """{"type":"my-type","_id":"my-doc-id","_rev":"my-doc-id","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"}"""
        }

        testCaseAsync "Serializes a find selector" <| async {
            let options: FindOption list = 
                [
                    Fields ["_id"] 
                    SortBy [Sort("_id", Ascending); Sort("_rev", Descending)] 
                    FindLimit 10
                    FindOption.Skip 3
                    UseIndex (Map.empty |> Map.add "not_sure" (Map.empty |> Map.add "some_key" 5))
                ]
            let selector: FindSelector = 
                Map.empty 
                |> Map.add "fieldName" [EqualTo 5; NotEqualTo "test"; GreaterThan (Map.empty |> Map.add "some_key" 5); LesserThan 20; GreaterThanOrEqualTo 1; LessThanOrEqualTo 15]
                
            selector 
            |> converter.WriteFindSelector options 
            |> Expect.equal "Should serialize a find selector" """{"fields":["_id"],"sort":[{"_id":"asc"},{"_rev":"desc"}],"limit":10,"skip":3,"use_index":{"not_sure":{"some_key":5}},"selector":{"fieldName":{"$eq":5,"$ne":"test","$gt":{"some_key":5},"$lt":20,"$gte":1,"$lte":15}}}"""
        }
    ]
