module Tests.Conveters

open Utils
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

    testList "Davenport.Fsharp.Converters" [
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
                    FindOption.UseIndex (FromDesignDocAndIndex ("DesignDocName", "IndexName"))
                ]
            let selector: FindSelector = 
                Map.empty 
                |> Map.add "fieldName" [EqualTo 5; NotEqualTo "test"; GreaterThan (Map.empty |> Map.add "some_key" 5); LesserThan 20; GreaterThanOrEqualTo 1; LessThanOrEqualTo 15]
                
            selector 
            |> converter.WriteFindSelector options 
            |> Expect.equal "Should serialize a find selector" """{"fields":["_id"],"sort":[{"_id":"asc"},{"_rev":"desc"}],"limit":10,"skip":3,"use_index":["DesignDocName","IndexName"],"selector":{"fieldName":{"$eq":5,"$ne":"test","$gt":{"some_key":5},"$lt":20,"$gte":1,"$lte":15}}}"""
        }

        testCaseAsync "Serializes indexes" <| async {
            ["field1"; "field2"; "field3"]
            |> converter.WriteIndexes [IndexOption.Name "index-name"]
            |> Expect.equal "Should serialize indexes" """{"index":{"fields":["field1","field2","field3"]},"name":"index-name"}"""
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

        testCaseAsync "Deserializes as a JObject" <| async {
            let (typeName, token) = 
                """{"couchdb":"Welcome","version":"2.0.0","vendor":{"name":"The Apache Software Foundation"}}"""
                |> JsonParseKind.JsonString
                |> converter.ReadAsJToken mapping

            Expect.equal "TypeName should be none" None typeName
            
            token.Value<string> "couchdb"
            |> Expect.equal "json prop `couchdb` should equal \"Welcome\"" "Welcome"
        }

        testCaseAsync "Deserializes database version" <| async {
            """{"couchdb":"Welcome","version":"2.0.0","vendor":{"name":"The Apache Software Foundation"}}"""
            |> JsonString
            |> converter.ReadVersionToken 
            |> Expect.equal "Should deserialize database version" "2.0.0"
        }

        testCaseAsync "Deserializes json as a Document with type name" <| async {
            let doc = 
                """{"type":"my-type","_id":"my-doc-id","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"}"""
                |> JsonString
                |> converter.ReadAsDocument mapping

            doc.TypeName
            |> Expect.equal "TypeName should be Some (my-type)" (Some "my-type")

            let doc = doc.ToObject<MyDoc>()

            Expect.equal "Deserialized doc should equal default doc" defaultDoc doc
        }

        testCaseAsync "Deserializes json as a Document without a type name" <| async {
            let doc = 
                """{"_id":"my-doc-id","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"}"""
                |> JsonString
                |> converter.ReadAsDocument mapping

            doc.TypeName
            |> Expect.equal "TypeName should be None" None

            let doc = doc.ToObject<MyDoc>()

            Expect.equal "Deserialized doc should equal default doc with null id and rev values (because without a typename the converter doesn't know which fields _id and _rev map to)." ({defaultDoc with Id = null; Rev = null}) doc
        }

        testCaseAsync "Deserializes a view result without values or docs" <| async {
            let viewResult = 
                """{"total_rows":133970,"offset":3,"rows":[{"id":"4_imported","key":[0,1354773600000],"value":null},{"id":"10_imported","key":[0,1354860000000],"value":null},{"id":"11_imported","key":[0,1354860000000],"value":null}]}"""
                |> JsonString
                |> converter.ReadAsViewResult mapping

            viewResult.TotalRows
            |> Expect.equal "Should return correct total row count" 133970

            viewResult.Offset
            |> Expect.equal "Should return correct offset" 3

            viewResult.Rows
            |> Seq.length
            |> Expect.equal "Should return 3 rows" 3

            viewResult.Rows
            |> Seq.map (fun r -> r.Key)
            |> Expect.all "All rows should have a key array" (function | ViewKey.List _ -> true | _ -> false)

            viewResult.Rows
            |> Seq.map (fun r -> r.Value)
            |> Expect.all "All rows should have a None .Value" Option.isNone

            viewResult.Rows 
            |> Seq.map (fun r -> r.Doc)
            |> Expect.all "All rows should have a None .Doc" Option.isNone

            viewResult.Rows
            |> Seq.map (fun r -> r.Id)
            |> Expect.all "All rows should have an id" (fun i -> String.length i > 0)
        }

        testCaseAsync "Deserializes a view result with values but without docs" <| async {
            let viewResult = 
                """{"total_rows":133970,"offset":3,"rows":[{"id":"4_imported","key":[0,1354773600000],"value":3},{"id":"10_imported","key":[0,1354860000000],"value":2},{"id":"11_imported","key":[0,1354860000000],"value":1}]}"""
                |> JsonString
                |> converter.ReadAsViewResult mapping

            viewResult.TotalRows
            |> Expect.equal "Should return correct total row count" 133970

            viewResult.Offset
            |> Expect.equal "Should return correct offset" 3

            viewResult.Rows
            |> Seq.length
            |> Expect.equal "Should return 3 rows" 3

            viewResult.Rows
            |> Seq.map (fun r -> r.Key)
            |> Expect.all "All rows should have a key array" (function | ViewKey.List _ -> true | _ -> false)

            viewResult.Rows
            |> Seq.map (fun r -> r.Value |> Option.map (fun v -> v.Raw.Type = Linq.JTokenType.Integer))
            |> Expect.allEqual "All rows should have a Some value" (Some true)

            viewResult.Rows 
            |> Seq.map (fun r -> r.Doc)
            |> Expect.all "All rows should have a None .Doc" Option.isNone

            viewResult.Rows
            |> Seq.map (fun r -> r.Id)
            |> Expect.all "All rows should have an id" (fun i -> String.length i > 0)
        }

        testCaseAsync "Deserializes a view result with single item keys" <| async {
            let viewResult = 
                """{"total_rows":133970,"offset":3,"rows":[{"id":"4_imported","key":1354773600000,"value":3},{"id":"10_imported","key":1354860000000,"value":2},{"id":"11_imported","key":1354860000000,"value":1}]}"""
                |> JsonString
                |> converter.ReadAsViewResult mapping

            viewResult.TotalRows
            |> Expect.equal "Should return correct total row count" 133970

            viewResult.Offset
            |> Expect.equal "Should return correct offset" 3

            viewResult.Rows
            |> Seq.length
            |> Expect.equal "Should return 3 rows" 3

            viewResult.Rows
            |> Seq.map (fun r -> r.Key)
            |> Expect.all "All rows should have a single-item key" (function | ViewKey.List _ -> false | _ -> true)

            viewResult.Rows
            |> Seq.map (fun r -> r.Value |> Option.map (fun v -> v.Raw.Type = Linq.JTokenType.Integer))
            |> Expect.allEqual "All rows should have a Some value" (Some true)

            viewResult.Rows 
            |> Seq.map (fun r -> r.Doc)
            |> Expect.all "All rows should have a None .Doc" Option.isNone

            viewResult.Rows
            |> Seq.map (fun r -> r.Id)
            |> Expect.all "All rows should have an id" (fun i -> String.length i > 0)
        }

        testCaseAsync "Deserializes a view result with docs" <| async {
            let viewResult = 
                """{"total_rows":133970,"offset":3,"rows":[{"id":"4_imported","key":1354773600000,"value":3,"doc":{"type":"my-type","_id":"my-doc-id","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"}},{"id":"10_imported","key":1354860000000,"value":2,"doc":{"type":"my-type","_id":"my-doc-id","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"}},{"id":"11_imported","key":1354860000000,"value":1,"doc":{"type":"my-type","_id":"my-doc-id","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"}}]}"""
                |> JsonString
                |> converter.ReadAsViewResult mapping

            viewResult.TotalRows
            |> Expect.equal "Should return correct total row count" 133970

            viewResult.Offset
            |> Expect.equal "Should return correct offset" 3

            viewResult.Rows
            |> Seq.length
            |> Expect.equal "Should return 3 rows" 3

            viewResult.Rows
            |> Seq.map (fun r -> r.Key)
            |> Expect.all "All rows should have a single-item key" (function | ViewKey.List _ -> false | _ -> true)

            viewResult.Rows
            |> Seq.map (fun r -> r.Value |> Option.map (fun v -> v.Raw.Type = Linq.JTokenType.Integer))
            |> Expect.allEqual "All rows should have a Some value" (Some true)

            viewResult.Rows 
            |> Seq.map (fun r -> r.Doc)
            |> Expect.all "All rows should have a Some .Doc" Option.isSome

            viewResult.Rows
            |> Seq.map (fun r -> r.Doc |> Option.map (fun d -> d.To<MyDoc>()) |> Option.get)
            |> Expect.allEqual "All docs should equal defaultDoc" defaultDoc

            viewResult.Rows
            |> Seq.map (fun r -> r.Id)
            |> Expect.all "All rows should have an id" (fun i -> String.length i > 0)
        }

        testCaseAsync "Deserializes a PostPutCopyResponse" <| async {
            let resp = 
                """{"ok":true,"id":"df8894a5b05cebf60df77bedee000e53","rev":"1-15f65339921e497348be384867bb940f"}"""
                |> JsonString
                |> converter.ReadAsPostPutCopyResponse

            resp.Okay
            |> Expect.isTrue ".ok should be true"

            resp.Id 
            |> Expect.equal "Id should equal the expected value" "df8894a5b05cebf60df77bedee000e53"

            resp.Rev 
            |> Expect.equal "Rev should equal the expected value" "1-15f65339921e497348be384867bb940f"
        }

        testCaseAsync "Deserializes a find result" <| async {
            let warning, docs = 
                """
                {
                    "docs": [
                        {"type":"my-type","_id":"FishStew","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"},
                        {"type":"my-type","_id":"PotatoStew","_rev":"my-doc-rev","Foo":true,"Bar":17,"Hello":"world","Token":"test token","Thing":{"Descended":15},"OtherThing":"SomethingElse"}
                    ],
                    "execution_stats": {
                        "total_keys_examined": 0,
                        "total_docs_examined": 200,
                        "total_quorum_docs_examined": 0,
                        "results_returned": 2,
                        "execution_time_ms": 5.52
                    }
                }
                """
                |> JsonString
                |> converter.ReadAsFindResult mapping

            warning 
            |> Expect.isNone "Warning should be None"

            docs 
            |> Seq.length 
            |> Expect.equal "Should only have 2 docs" 2

            docs 
            |> Expect.all "All docs should have 'my-type' type." (fun d -> d.TypeName = Some "my-type")

            let fishStew = 
                docs 
                |> Seq.map (fun d -> d.To<MyDoc>())
                |> Seq.filter (fun d -> d.Id = "FishStew")
                |> Seq.head

            fishStew 
            |> Expect.equal "Should equal defaultDoc with Id=FishStew" ({defaultDoc with Id = "FishStew"})

            let potatoStew =
                docs 
                |> Seq.map (fun d -> d.To<MyDoc>())
                |> Seq.filter (fun d -> d.Id = "PotatoStew")
                |> Seq.head

            potatoStew
            |> Expect.equal "Should equal defaultDoc with Id=PotatoStew" ({defaultDoc with Id = "PotatoStew"})
        }

        testCaseAsync "Deserializes a FindResult with a warning and no docs" <| async {
            let warning, docs = 
                """
                {
                    "docs": [],
                    "warning": "Hello world",
                    "execution_stats": {
                        "total_keys_examined": 0,
                        "total_docs_examined": 200,
                        "total_quorum_docs_examined": 0,
                        "results_returned": 2,
                        "execution_time_ms": 5.52
                    }
                }
                """
                |> JsonString
                |> converter.ReadAsFindResult mapping

            warning 
            |> Expect.equal "Warning should be Some" (Some "Hello world")

            docs 
            |> Seq.length 
            |> Expect.equal "Should have 0 docs" 0
        }

        testCaseAsync "Deserializes a BulkResult list" <| async {
            let result = 
                """
                [
                    {
                        "id": "FishStew",
                        "ok": true,
                        "rev": "2-2bff94179917f1dec7cd7f0209066fb8"
                    },
                    {
                        "id" : "PotatoStew",
                        "error" : "conflict",
                        "reason" : "Document update conflict."
                    },
                    {
                        "id" : "TurkeyStew",
                        "error" : "forbidden",
                        "reason" : "A reason that this is forbidden."
                    },
                    {
                        "id" : "ChickenStew",
                        "error" : "hello_world",
                        "reason" : "This is a fake error.",
                        "rev": "A-fake-rev"
                    }
                ]
                """
                |> JsonString
                |> converter.ReadAsBulkResultList
              
            result
            |> Seq.tryFind (function | Failed _ -> true | Inserted _ -> false)
            |> Expect.isSome "Should contain a Failed union"

            result 
            |> Seq.tryFind (function | Failed _ -> false | Inserted _ -> true)
            |> Expect.isSome "Should contain an Inserted union"

            result 
            |> Seq.filter (function | Failed _ -> true | Inserted _ -> false)
            |> Seq.length
            |> Expect.equal "Should contain 3 Failed" 3

            result 
            |> Seq.filter (function | Failed _ -> false | Inserted _ -> true)
            |> Seq.length
            |> Expect.equal "Should contain 1 Inserted" 1

            let fishStew = 
                result 
                |> Seq.findSome (function | Failed _ -> None | Inserted i -> (if i.Id = "FishStew" then Some i else None))
                |> Option.get

            fishStew.Id
            |> Expect.equal "" "FishStew"

            fishStew.Rev 
            |> Expect.equal "" "2-2bff94179917f1dec7cd7f0209066fb8"

            fishStew.Okay
            |> Expect.isTrue "FishStew's .Okay should be true"

            let potatoStew = 
                result 
                |> Seq.findSome (function | Failed e -> (if e.Id = "PotatoStew" then Some e else None) | _ -> None)
                |> Option.get

            potatoStew.Id 
            |> Expect.equal "" "PotatoStew"

            potatoStew.Rev 
            |> Expect.isNone "PotatoStew should not have a Rev"

            potatoStew.Reason
            |> Expect.equal "" "Document update conflict."

            potatoStew.Error
            |> Expect.equal "" BulkErrorType.Conflict

            let turkeyStew = 
                result 
                |> Seq.findSome (function | Failed e -> (if e.Id = "TurkeyStew" then Some e else None) | _ -> None)
                |> Option.get

            turkeyStew.Id
            |> Expect.equal "" "TurkeyStew"

            turkeyStew.Rev
            |> Expect.isNone "TurkeyStew should not have a Rev"

            turkeyStew.Reason
            |> Expect.equal "" "A reason that this is forbidden."

            turkeyStew.Error
            |> Expect.equal "" BulkErrorType.Forbidden

            let chickenStew = 
                result 
                |> Seq.findSome (function | Failed e -> (if e.Id = "ChickenStew" then Some e else None) | _ -> None)
                |> Option.get

            chickenStew.Id
            |> Expect.equal "" "ChickenStew"

            chickenStew.Rev
            |> Expect.equal "ChickenStew should have a Rev" (Some "A-fake-rev") 

            chickenStew.Reason
            |> Expect.equal "" "This is a fake error."

            chickenStew.Error
            |> Expect.equal "" (BulkErrorType.Other "hello_world")
        }

        testCaseAsync "Deserializes an IndexInsertResult" <| async {
            let result = 
                """{"result":"created","id":"_design/a59b81275aafc24187371e9f965af971d06620f3","name":"davenport_net_fsharp-indexes"}"""
                |> JsonString
                |> converter.ReadAsIndexInsertResult

            result.Id 
            |> Expect.equal "" "_design/a59b81275aafc24187371e9f965af971d06620f3"

            result.Name 
            |> Expect.equal "" "davenport_net_fsharp-indexes"

            result.Result 
            |> Expect.equal "" CreateResult.Created
        }
    ]
