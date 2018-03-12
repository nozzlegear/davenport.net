module Davenport.Fsharp.Tests.Library

open System
open Davenport.Fsharp
open Davenport.Types
open Expecto
open Expecto.Flip

type MyUnion =
    | Case1 of string
    | Case2 of int
    | Case3 of int64

[<Literal>]
let MyTestClassType = "my-test-class"

type MyTestClass = 
  { MyId: string
    MyRev: string
    Foo: string
    Bar: bool
    Baz: int
    Bat: int64
    Opt: string option
    Union: MyUnion
    Date: DateTime
    Dec: Decimal }
  with 
  static member typeName = MyTestClassType

[<Literal>]
let MyOtherClassType = "my-other-class"

type MyOtherClass = 
    { DocId: string
      Revision: string 
      hello: bool }
    with 
    static member typeName = MyOtherClassType

let defaultRecord = {
    MyId = ""
    MyRev = ""
    Foo = "test value"
    Bar = true
    Baz = 11
    Bat = 0L
    Opt = None
    Union = Case1 "Hello world"
    Date = DateTime.UtcNow
    Dec = 10.5m
}

let defaultSecondRecord = {
    DocId = ""
    Revision = ""
    hello = true
}

type DatabaseDoc = 
    | FirstDoc of MyTestClass
    | SecondDoc of MyOtherClass

let insertable (doc: DatabaseDoc) : InsertedDocument<obj> =
    match doc with 
    | FirstDoc d -> Some MyTestClass.typeName, d :> obj
    | SecondDoc d -> Some MyTestClass.typeName, d :> obj

let defaultInsert = defaultRecord |> FirstDoc |> insertable

let defaultSecondInsert = defaultSecondRecord |> SecondDoc |> insertable

// let defaultDesignDocs =
//     mapFunction "function (doc) { if (doc.Baz > 10) { emit(doc._id, doc); } }"
//     |> reduceFunction "_count"
//     |> view viewName
//     |> toSeq
//     |> designDoc designDocName
//     |> toSeq

let notNullOrEmpty message = String.IsNullOrEmpty >> Expect.isFalse message

let asyncMap (fn: 'a -> 'b) (task: Async<'a>) = async {
    let! result = task

    return fn result
}

let mapDoc ((typeName, data): Document) = 
    match typeName with 
    | Some MyOtherClassType -> 
        data.ToObject<MyOtherClass>()
        |> SecondDoc
    | Some MyTestClassType -> 
        data.ToObject<MyTestClass>()
        |> FirstDoc
    | Some _
    | None -> failwithf "Failed to map unknown document %A" data

let mapFirstDoc = mapDoc >> function 
    | FirstDoc d -> d
    | SecondDoc _ -> failwith "Expected FirstDoc."

let mapSecondDoc = mapDoc >> function 
    | FirstDoc _ -> failwith "Expected SecondDoc."
    | SecondDoc d -> d

let mapListToFirstDocs docs =
    docs
    |> Seq.map (fun d -> try mapFirstDoc d |> Some with _ -> None)
    |> Seq.filter Option.isSome
    |> Seq.map Option.get    

let mapListToSecondDocs docs = 
    docs
    |> Seq.map (fun d -> try mapSecondDoc d |> Some with _ -> None)
    |> Seq.filter Option.isSome
    |> Seq.map Option.get

[<Tests>]
let tests =
    let debug = false
    let fiddler = false
    let url = if fiddler then "localhost.fiddler:5984" else System.Environment.GetEnvironmentVariable "COUCHDB_URL"
    let client =
        url
        |> database "davenport_net_fsharp"
        |> mapFields (Map.ofSeq [MyTestClass.typeName, ("MyId", "MyRev"); MyOtherClass.typeName, ("DocId", "Revision")])
        |> warning (printfn "%s")

    // Set debug to `true` to start debugging.
    // 1. Start the test suite (dotnet run)
    // 2. Go to VS Code's Debug tab.
    // 3. Choose ".NET Core Attach"
    // 4. Choose one of the running processes. It's probably the one that says 'dotnet exec yadayada path to app'. Several processes may start and stop while the project is building.
    if debug then
        printfn "Waiting to attach debugger. Run .NET Core Attach under VS Code's debug menu."
        while not(System.Diagnostics.Debugger.IsAttached) do
          System.Threading.Thread.Sleep(100)
        System.Diagnostics.Debugger.Break()

    printfn "Creating database at url %s." url

    createDatabase client
    |> Async.RunSynchronously
    |> ignore

    printfn "Database created."

    testList "Davenport.Fsharp.Wrapper" [
        testCaseAsync "Gets a document's raw json" <| async {
            let! (createdId, createdRev, _) = create defaultInsert client
            let! json = getRaw createdId (Some createdRev) client

            String.IsNullOrEmpty json
            |> Expect.isFalse "JSON string should not be empty"
        }

        testCaseAsync "Gets a document of the first type" <| async {
            let! (createdId, createdRev, _) = create defaultInsert client
            let! doc = 
                get createdId (Some createdRev) client
                |> asyncMap mapFirstDoc

            notNullOrEmpty "Id should not be empty" doc.MyId
            notNullOrEmpty "Rev should not be empty" doc.MyRev
            Expect.equal "" defaultRecord.Foo doc.Foo
            Expect.equal "" defaultRecord.Bar doc.Bar
            Expect.equal "" defaultRecord.Baz doc.Baz
        }

        testCaseAsync "Gets a document of the second type" <| async {
            let! (createdId, createdRev, _) = create defaultSecondInsert client
            let! doc = 
                get createdId (Some createdRev) client
                |> asyncMap mapSecondDoc

            notNullOrEmpty "Id should not be empty" doc.DocId
            notNullOrEmpty "Rev should not be empty" doc.Revision
            Expect.equal ".hello should match" defaultSecondRecord.hello doc.hello
        }

        testCaseAsync "Bulk insert" <| async {
            skiptest "Not implemented"
        }

        testCaseAsync "Creates docs" <| async {
            let! (docId, docRev, ok) = create defaultInsert client

            notNullOrEmpty "Id was empty" docId
            notNullOrEmpty "Rev was empty" docRev
            Expect.isTrue "" ok
        }

        testCaseAsync "Creates docs with specific ids" <| async {
            let myId = sprintf "specific_id_%s" (Guid.NewGuid().ToString())
            let! (docId, docRev, ok) = createWithId myId defaultInsert client

            Expect.equal "Created id and supplied id did not match" docId myId
            notNullOrEmpty "Rev was empty" docRev
            Expect.isTrue "" ok
        }

        testCaseAsync "Gets docs without a revision" <| async {
            let! (createdId, _, _) = create defaultInsert client
            let! doc = 
                get createdId None client
                |> asyncMap mapFirstDoc

            notNullOrEmpty "Id should not be empty" doc.MyId
            notNullOrEmpty "Rev should not be empty" doc.MyRev
            Expect.equal "" defaultRecord.Foo doc.Foo
            Expect.equal "" defaultRecord.Bar doc.Bar
            Expect.equal "" defaultRecord.Baz doc.Baz
        }

        testCaseAsync "Throws 404 for docs that don't exist" <| async {
            let! exists = 
                get (Guid.NewGuid().ToString()) None client
                |> Async.Catch
                |> asyncMap (function | Choice2Of2 (:? DavenportException as exn) when exn.StatusCode = 404 -> false | _ -> true)

            Expect.isFalse "Should be false" exists
        }

        testCaseAsync "Counts docs" <| async {
            do! create defaultInsert client |> Async.Ignore

            let! count = count client

            Expect.isGreaterThan "Count should be greater than 0" (count, 0)
        }

        // testCaseAsync "Counts docs with an expression" <| async {
        //     let expected = "counts_with_expression"
        //     let record = { defaultRecord with Foo = expected }

        //     do! create<MyTestClass> record client |> Async.Ignore

        //     let! exprCount = countByExpr (<@ fun (r: MyTestClass) -> r.Foo = expected @>) client
        //     let! totalCount = count client

        //     Expect.isGreaterThan exprCount 0 "Count was 0."
        //     Expect.isLessThanOrEqual exprCount totalCount "Filtered count was not less than or equal to the count of all docs."
        // }

        testCaseAsync "Counts docs with a selector" <| async {
            let expected = "counts_with_map"
            let insert = 
                { defaultRecord with Foo = expected }
                |> FirstDoc 
                |> insertable

            do! create insert client |> Async.Ignore

            let map = Map.ofSeq ["Foo", [EqualTo expected]]

            let! mapCount = countBySelector map client
            let! totalCount = count client

            Expect.isGreaterThan "Count should be greater than 0" (mapCount, 0)
            Expect.isLessThanOrEqual "Filtered count was not less than or equal to the count of all docs." (mapCount, totalCount)
        }

        testCaseAsync "Updates docs" <| async {
            let! (createdId, createdRev, _) = create defaultInsert client
            let! retrieved =
                get createdId (Some createdRev) client
                |> asyncMap mapFirstDoc

            let newFoo = "updated_with_davenport_fsharp_wrapper"
            let newData = 
                { retrieved with Foo = newFoo }
                |> FirstDoc
                |> insertable
            let! (id, rev, ok) = update createdId createdRev newData client

            Expect.isTrue "Update result is not Ok." ok

            let! updated =
                get id (Some rev) client
                |> asyncMap mapFirstDoc

            Expect.equal updated.Foo newFoo "Failed to update doc's Foo property."
        }

        testCaseAsync "Deletes docs" <| async {
            let! (id, rev, _) = create defaultInsert client

            do! delete id rev client
        }

        testCaseAsync "Retrieves all docs" <| async {
            // Create at least one doc to list
            do! create defaultInsert client |> Async.Ignore

            let! (totalRows, offset, docs) = allDocs WithDocs [] client

            Expect.equal "List offset is not 0." offset 0
            Expect.isTrue "Total rows should be greater than 0" (totalRows > 0)
            Expect.isNonEmpty "List is empty." docs

            let mappedDocs = 
                docs
                |> Seq.map (fun d -> try mapDoc d |> Some with _ -> None)
                |> Seq.filter Option.isSome
                |> Seq.map Option.get
            
            let hasId d = 
                match d with
                | FirstDoc d -> d.MyId
                | SecondDoc d -> d.DocId
                |> String.IsNullOrEmpty
                |> not

            let hasRev d = 
                match d with 
                | FirstDoc d -> d.MyRev
                | SecondDoc d -> d.Revision
                |> String.IsNullOrEmpty 
                |> not

            Expect.all "No doc should have an empty id" hasId mappedDocs
            Expect.all "No doc should have an empty rev" hasRev mappedDocs
        }

        testCaseAsync "Lists without docs" <| async {
            skiptest "Test not implemented. Must make list function return document revisions"
            // // Create at least one doc to list
            // do! create defaultRecord client |> Async.Ignore

            // let! list = listWithoutDocs None client

            // Expect.equal list.Offset 0 "List offset is not 0."
            // Expect.isNonEmpty list.Rows "List is empty"
            // Expect.all list.Rows (fun r -> r.Doc.GetType() = typeof<Davenport.Entities.Revision>) "All docs should be of type Revision"
            // Expect.all list.Rows (fun r -> not <| r.Id.StartsWith "_design") "No doc should start with _design"
            // Expect.all list.Rows (fun r -> not <| String.IsNullOrEmpty r.Doc.Rev) "All docs should have a Rev property"
        }

        // testCaseAsync "Finds docs with an expression" <| async {
        //     let expected = "finds_docs_with_expression"

        //     do! create {defaultRecord with Foo = expected} client |> Async.Ignore

        //     let! found = findByExpr <@ fun (c: MyTestClass) -> c.Foo = expected @> None client

        //     Expect.isNonEmpty found "findByExpr should have found at least one doc."
        //     Expect.all found (fun c -> c.Foo = expected) "All docs returned by findByExpr should have the expected Foo value."
        // }

        // testCaseAsync "Finds docs with a notequal expression" <| async {
        //     let expected = "finds_docs_with_a_notequal_expression"

        //     do! create ({defaultRecord with Foo = expected}) client |> Async.Ignore
        //     do! create defaultRecord client |> Async.Ignore

        //     let! found = findByExpr <@ fun (c: MyTestClass) -> c.Foo <> expected @> None client

        //     Expect.isNonEmpty found "findByExpr should have found at least one doc."
        //     Expect.all found (fun c -> c.Foo <> expected) "All docs returned should not have a Foo value equal to the search value."
        // }

        testCaseAsync "Finds docs with a map" <| async {
            let expected = "finds_with_map"
            let insert = 
                { defaultRecord with Foo = expected }
                |> FirstDoc
                |> insertable

            do! create insert client |> Async.Ignore

            let map = Map.ofSeq ["Foo", [EqualTo expected]; "type", [EqualTo MyTestClass.typeName]]
            let! found = 
                find [] map client
                |> asyncMap mapListToFirstDocs

            Expect.isTrue "Should have found at least one MyTestClass doc" (Seq.length found > 0)
            Expect.all "All docs returned should have the expected Foo value." (fun c -> c.Foo = expected) found
        }

        testCaseAsync "Finds docs with a notequal map" <| async {
            let expected = "finds_docs_with_a_notequal_map"
            let insert = 
                { defaultRecord with Foo = expected }
                |> FirstDoc
                |> insertable

            do! create insert client |> Async.Ignore
            do! create defaultInsert client |> Async.Ignore

            let map = Map.ofSeq ["Foo", [NotEqualTo expected]; "type", [EqualTo MyTestClass.typeName]]
            let! found = 
                find [] map client
                |> asyncMap mapListToFirstDocs

            Expect.isNonEmpty "findByMap should have found at least MyTestClass doc." found
            Expect.all "All docs returned should not have a Foo value equal to the search value." (fun c -> c.Foo <> expected) found
        }

        testCaseAsync "Doc exists" <| async {
            let! id, rev, _ = create defaultInsert client
            let! existsWithRev = exists id (Some rev) client
            let! existsWithoutRev = exists id None client

            Expect.isTrue "Exists without rev should be true" existsWithoutRev
            Expect.isTrue "Exists with rev should be true" existsWithRev
        }

        // testCaseAsync "Doc exists by expression" <| async {
        //     let expected = "doc_exists_by_expr"

        //     do! create ({defaultRecord with Foo = expected}) client |> Async.Ignore

        //     let! exists = existsByExpr <@ fun (c: MyTestClass) -> c.Foo = expected @> client

        //     Expect.isTrue exists ""
        // }

        testCaseAsync "Doc exists by selector" <| async {
            let expected = "doc_exists_by_selector"
            let insert = 
                { defaultRecord with Foo = expected }
                |> FirstDoc
                |> insertable

            do! create insert client |> Async.Ignore

            let map = Map.ofSeq ["Foo", [EqualTo expected]]
            let! exists = existsBySelector map client

            Expect.isTrue "" exists
        }

        testCaseAsync "Copies docs" <| async {
            let uuid = sprintf "a-unique-string-%i" DateTime.UtcNow.Millisecond
            let! id, _, _ = create defaultInsert client
            let! id, _, ok = copy id uuid client

            Expect.isTrue "" ok
            Expect.equal "The copied document's id should have equaled the expected uuid." id uuid
        }

        testCaseAsync "Creates and deletes databases" <| async {
            let name = "davenport_fsharp_delete_me"
            let client = "localhost:5984" |> database name

            do! createDatabase client |> Async.Ignore

            // Create the database again to ensure it doesn't fail if the database already existed
            let! createResult = createDatabase client

            match createResult with 
            | AlreadyExisted -> true
            | Created -> false
            |> Expect.isTrue "Should have returned an 'AlreadyExisted' union type."

            do! deleteDatabase client
        }

        testCaseAsync "Warnings work" <| async {
            let mutable called = false
            let handleWarning _ =
                called <- true

            let clientWithWarning = client |> warning handleWarning
            let! id, _, _ = create defaultInsert clientWithWarning

            // To get a warning, attempt to find a document by searching for a field that doesn't have an index.
            do!
                clientWithWarning
                |> find [] (Map.ofSeq ["my-dude-that-doesn't-exist", [EqualTo "henlo"]])
                |> Async.Catch
                |> Async.Ignore

            Expect.isTrue "Warning event was not called" called
        }

        testCaseAsync "Creates design docs and gets view results" <| async {
            // Make sure the design docs exist
            let views: View list = [
                "my-view", "function (doc) { if (doc.Baz > 10) { emit(doc._id, doc); } }", None
            ]
            let designDoc = "_design/henlo", views
            do! createOrUpdateDesignDoc defaultDesignDocs client

            // Create at least one doc that would match
            do! create ({defaultRecord with Baz = 15}) client |> Async.Ignore
            let! viewResult = executeView<int> designDocName viewName None client

            Expect.isGreaterThan (Seq.length viewResult) 0 "View should return at least one result"
            Expect.isGreaterThanOrEqual (viewResult |> Seq.sumBy (fun d -> d.Value)) 10 "The sum of all doc values should be greater than 10"
        }

        testCaseAsync "Finds values serialized by Fable.JsonConverter" <| async {
            // Fable.JsonConverter handles serialization much more differently than Json.Net.
            // For instance, an int64 is serialized as '+1234567' string. The problem arises when
            // using the find methods, because Davenport isn't serializing an FsDoc -- it's serializing
            // a dictionary of find options. Previously these options would never get passed to the custom
            // converter and therefore we'd get Json.Net searching for an int64 and couchdb only using strings.
            let expected = 12345678987654321L
            do! create ({ defaultRecord with Bat = expected }) client |> Async.Ignore

            let! findResult = findByExpr <@ fun (d: MyTestClass) -> d.Bat = expected @> None client

            Expect.isGreaterThan (Seq.length findResult) 0 "Should have returned at least one record."
            Expect.all findResult (fun d -> d.Bat = expected) "Every returned doc should have a Bat property equal to the expected value."
        }

        testCaseAsync "Can find values between int64" <| async {
            let expected = 1516395484000L
            let min = expected - 10000000000L
            let max = expected + 10000000000L

            // Create records that would be under the min range, over the max range, and between both
            do! Async.Parallel [
                    create ({defaultRecord with Bat = min - 1L}) client
                    create ({defaultRecord with Bat = max + 1L}) client
                    create ({defaultRecord with Bat = expected - 1L}) client
                    create ({defaultRecord with Bat = expected + 1L}) client
                ]|> Async.Ignore

            // let! result = findByExpr<MyTestClass> <@ fun (c: MyTestClass) -> c.Bat > min @> None client
            let selector = Map.ofSeq ["Bat", [GreaterThan min; LesserThan max]]
            let! result = findBySelector<MyTestClass> selector None client

            Expect.all result (fun c -> c.Bat > min && c.Bat < max) "All records returned should be greater than min value and lesser than max value."
        }

        testCaseAsync "Serializes and deserializes options and unions" <| async {
            let expectedOpt = None
            let expectedUnionStr = Case1 "testing union 1"
            let expectedUnionInt = Case2 42
            let expectedUnionInt64 = Case3 123456789L
            let expectedDate = DateTime.UtcNow.AddHours -7.
            let expectedDecimal = 23.75M

            let! createdOpt = create ({ defaultRecord with Opt = expectedOpt }) client
            let! createdUnionStr = create ({ defaultRecord with Union = expectedUnionStr }) client
            let! createdUnionInt = create ({defaultRecord with Union = expectedUnionInt }) client
            let! createdUnionInt64 = create ({defaultRecord with Union = expectedUnionInt64 }) client
            let! createdDateTime = create ({defaultRecord with Date = expectedDate}) client
            let! createdDecimal = create({defaultRecord with Dec = expectedDecimal}) client

            let! opt = get createdOpt.Id (Some createdOpt.Rev) client |> asyncMap Option.get
            let! unionStr = get createdUnionStr.Id (Some createdUnionStr.Rev) client |> asyncMap Option.get
            let! unionInt = get createdUnionInt.Id (Some createdUnionInt.Rev) client |> asyncMap Option.get
            let! unionInt64 = get createdUnionInt64.Id (Some createdUnionInt64.Rev) client |> asyncMap Option.get
            let! dateTime = get createdDateTime.Id (Some createdDateTime.Rev) client |> asyncMap Option.get
            let! decimal = get createdDecimal.Id (Some createdDecimal.Rev) client |> asyncMap Option.get

            Expect.equal opt.Opt expectedOpt "Options should be equal"
            Expect.equal unionStr.Union expectedUnionStr "Union str should be equal"
            Expect.equal unionInt.Union expectedUnionInt "Union int should be equal"
            Expect.equal unionInt64.Union expectedUnionInt64 "Union int64 should be equal"
            Expect.equal dateTime.Date expectedDate "Date should be equal"
            Expect.equal decimal.Dec expectedDecimal "Decimal should be equal"
        }
    ]
