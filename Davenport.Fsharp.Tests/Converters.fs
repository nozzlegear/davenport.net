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
    let insertable doc: InsertedDocument<MyDoc> = Some "my-type", doc
    let defaultDoc = 
        {
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

    ftestList "Davenport.Fsharp.Converters" [
        testCaseAsync "Serializes an InsertedDocument" <| async {
            insertable defaultDoc         
            |> converter.WriteInsertedDocument mapping
            |> Expect.equal "Should serialize to expected string" """{"type":"my-type","_id":"my-doc-id","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"}"""
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

        testCaseAsync "Serializes indexes" <| async {
            ["field1"; "field2"; "field3"]
            |> converter.WriteIndexes "index-name"
            |> Expect.equal "Should serialize indexes" """{"name":"index-name","fields":["field1","field2","field3"]}"""
        }

        testCaseAsync "Serializes design docs" <| async {
            Map.empty 
            |> Map.add "my-first-view" ("function (doc) { emit(doc._id) }", Some "_count")
            |> Map.add "my-second-view" ("function (doc) { emit(doc._id, 5) }", None)
            |> converter.WriteDesignDoc
            |> Expect.equal "Should serialize design docs" """{"views":{"my-first-view":{"reduce":"_count","map":"function (doc) { emit(doc._id) }"},"my-second-view":{"map":"function (doc) { emit(doc._id, 5) }"}},"language":"javascript"}"""
        }

        testCaseAsync "Serializes bulk inserts" <| async {
            [
                insertable defaultDoc 
                insertable { defaultDoc with Hello = "goodbye" }
            ]
            |> converter.WriteBulkInsertList mapping AllowNewEdits
            |> Expect.equal "Should serialize bulk insert list" """{"new_edits":true,"docs":[{"type":"my-type","_id":"my-doc-id","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"},{"type":"my-type","_id":"my-doc-id","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"goodbye","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"}]}"""
        }

        testCaseAsync "Deserializes database version" <| async {
            """{"couchdb":"Welcome","version":"2.0.0","vendor":{"name":"The Apache Software Foundation"}}"""
            |> converter.ReadVersionToken 
            |> Expect.equal "Should deserialize database version" "2.0.0"
        }

        testCaseAsync "Deserializes json as a Document with type name" <| async {
            let doc = 
                """{"type":"my-type","_id":"my-doc-id","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"}"""
                |> converter.ReadAsDocument mapping

            doc.TypeName
            |> Expect.equal "TypeName should be Some (my-type)" (Some "my-type")

            let doc = doc.ToObject<MyDoc>()

            Expect.equal "Deserialized doc should equal default doc" defaultDoc doc
        }

        testCaseAsync "Deserializes json as a Document without a type name" <| async {
            let doc = 
                """{"_id":"my-doc-id","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"}"""
                |> converter.ReadAsDocument mapping

            doc.TypeName
            |> Expect.equal "TypeName should be None" None

            let doc = doc.ToObject<MyDoc>()

            Expect.equal "Deserialized doc should equal default doc with null id and rev values (because without a typename the converter doesn't know which fields _id and _rev map to)." ({defaultDoc with Id = null; Rev = null}) doc
        }
    ]
