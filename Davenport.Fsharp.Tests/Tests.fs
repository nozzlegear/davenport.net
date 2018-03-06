module Tests

open System
open Expecto
open Davenport.Fsharp.Wrapper

type MyUnion =
    | Case1 of string
    | Case2 of int
    | Case3 of int64

type MyTestClass = {
    MyId: string
    MyRev: string
    Foo: string
    Bar: bool
    Baz: int
    Bat: int64
    Opt: string option
    Union: MyUnion
    Date: DateTime
    Dec: Decimal
}

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

let viewName = "only-bazs-greater-than-10"

let designDocName = "list"

let defaultDesignDocs =
    mapFunction "function (doc) { if (doc.Baz > 10) { emit(doc._id, doc); } }"
    |> reduceFunction "_count"
    |> view viewName
    |> toSeq
    |> designDoc designDocName
    |> toSeq

let notNullOrEmpty (s: string) = String.IsNullOrEmpty s |> Expect.isFalse

let asyncMap (fn: 'a -> 'b) (task: Async<'a>) = async {
    let! result = task

    return fn result
}

type FirstDoc = {
    MyId: string
    MyRev: string
    hello: bool
}

type SecondDoc = {
    MyId: string
    MyRev: string
    hello: int 
}

type EitherDoc = 
    | Doc1 of FirstDoc
    | Doc2 of SecondDoc

[<Tests>]
let tests =
    let debug = false
    let fiddler = false
    let url = if fiddler then "localhost.fiddler:5984" else System.Environment.GetEnvironmentVariable "COUCHDB_URL"
    let client =
        url
        |> database "davenport_net_fsharp"
        |> idField "MyId"
        |> revField "MyRev"

    // Set debug to `true` below to start debugging.
    // 1. Start the test suite (dotnet run)
    // 2. Go to VS Code's Debug tab.
    // 3. Choose ".NET Core Attach"
    // 4. Choose one of the running processes. It's probably the one that says 'dotnet exec yadayada path to app'. Several processes may start and stop while the project is building.
    if debug then
        printfn "Waiting to attach debugger. Run .NET Core Attach under VS Code's debug menu."
        while not(System.Diagnostics.Debugger.IsAttached) do
          System.Threading.Thread.Sleep(100)
        System.Diagnostics.Debugger.Break()

    printfn "Configuring database at url %s." url

    // Configure the database before running tests
    // configureDatabase [] [] client
    // |> Async.RunSynchronously

    printfn "Database configured."

    testList "Davenport.Fsharp.Wrapper" [
        testCaseAsync "Creates docs" <| async {
            let! doc = create<MyTestClass> defaultRecord client

            notNullOrEmpty doc.Id "Id was empty"
            notNullOrEmpty doc.Rev "Rev was empty"
            Expect.isTrue doc.Ok ""
        }

        testCaseAsync "Creates docs with specific ids" <| async {
            let myId = sprintf "specific_id_%s" (Guid.NewGuid().ToString())
            let! doc = createWithId myId defaultRecord client

            Expect.equal doc.Id myId "Created id and supplied id did not match"
            notNullOrEmpty doc.Rev "Rev was empty"
            Expect.isTrue doc.Ok ""
        }

        testCaseAsync "Gets docs" <| async {
            let! created = create<MyTestClass> defaultRecord client
            let! docResult = get<MyTestClass> created.Id None client

            Expect.isSome docResult "DocResult is None"

            let doc = Option.get docResult

            Expect.equal created.Id doc.MyId ""
            Expect.equal created.Rev doc.MyRev ""
            Expect.equal defaultRecord.Foo doc.Foo ""
            Expect.equal defaultRecord.Bar doc.Bar ""
            Expect.equal defaultRecord.Baz doc.Baz ""
        }

        testCaseAsync "Gets docs with revision" <| async {
            let! created = create<MyTestClass> defaultRecord client
            let! docResult = get<MyTestClass> created.Id (Some created.Rev) client

            Expect.isSome docResult "DocResult is None"

            let doc = Option.get docResult

            Expect.equal created.Id doc.MyId ""
            Expect.equal created.Rev doc.MyRev ""
            Expect.equal defaultRecord.Foo doc.Foo ""
            Expect.equal defaultRecord.Bar doc.Bar ""
            Expect.equal defaultRecord.Baz doc.Baz ""
        }

        testCaseAsync "Returns None for dogs that don't exist" <| async {
            let! docResult = get<MyTestClass> (Guid.NewGuid().ToString()) None client

            Expect.isNone docResult "DocResult should be None"
        }

        testCaseAsync "Counts docs" <| async {
            do! create<MyTestClass> defaultRecord client |> Async.Ignore

            let! count = count client

            Expect.isGreaterThan count 0 "Count was 0."
        }

        testCaseAsync "Counts docs with an expression" <| async {
            let expected = "counts_with_expression"
            let record = { defaultRecord with Foo = expected }

            do! create<MyTestClass> record client |> Async.Ignore

            let! exprCount = countByExpr (<@ fun (r: MyTestClass) -> r.Foo = expected @>) client
            let! totalCount = count client

            Expect.isGreaterThan exprCount 0 "Count was 0."
            Expect.isLessThanOrEqual exprCount totalCount "Filtered count was not less than or equal to the count of all docs."
        }

        testCaseAsync "Counts docs with a map" <| async {
            let expected = "counts_with_map"
            let record = { defaultRecord with Foo = expected }

            do! create record client |> Async.Ignore

            let map = Map.ofSeq ["Foo", [EqualTo expected]]

            let! mapCount = countBySelector map client
            let! totalCount = count client

            Expect.isGreaterThan mapCount 0 "Count was 0"
            Expect.isLessThanOrEqual mapCount totalCount "Filtered count was not less than or equal to the count of all docs."
        }

        testCaseAsync "Updates docs" <| async {
            let! created = create defaultRecord client
            let! retrieved =
                get created.Id (Some created.Rev) client
                |> asyncMap Option.get

            let newFoo = "updated_with_davenport_fsharp_wrapper"
            let! updateResult = update created.Id created.Rev ({ retrieved with Foo = newFoo}) client

            Expect.isTrue updateResult.Ok "Update result is not Ok."

            let! updated =
                get updateResult.Id (Some updateResult.Rev) client
                |> asyncMap Option.get

            Expect.equal updated.Foo newFoo "Failed to update doc's Foo property."
        }

        testCaseAsync "Deletes docs" <| async {
            let! created = create defaultRecord client

            do! delete created.Id created.Rev client
        }

        testCaseAsync "Lists with docs" <| async {
            // Create at least one doc to list
            do! create defaultRecord client |> Async.Ignore

            let! list = listWithDocs<MyTestClass> None client

            Expect.equal list.Offset 0 "List offset is not 0."
            Expect.isNonEmpty list.Rows "List is empty."
            Expect.all list.Rows (fun r -> r.Doc.GetType() = typeof<MyTestClass>) "All docs should be of type MyTestClass"
            Expect.all list.Rows (fun r -> not <| r.Id.StartsWith "_design") "No doc should start with _design"
            Expect.all list.Rows (fun r -> not <| String.IsNullOrEmpty r.Doc.MyId) "No doc should have an empty id"
            Expect.all list.Rows (fun r -> not <| String.IsNullOrEmpty r.Doc.MyRev) "No doc should have an empty rev"
        }

        testCaseAsync "Lists without docs" <| async {
            // Create at least one doc to list
            do! create defaultRecord client |> Async.Ignore

            let! list = listWithoutDocs None client

            Expect.equal list.Offset 0 "List offset is not 0."
            Expect.isNonEmpty list.Rows "List is empty"
            Expect.all list.Rows (fun r -> r.Doc.GetType() = typeof<Davenport.Entities.Revision>) "All docs should be of type Revision"
            Expect.all list.Rows (fun r -> not <| r.Id.StartsWith "_design") "No doc should start with _design"
            Expect.all list.Rows (fun r -> not <| String.IsNullOrEmpty r.Doc.Rev) "All docs should have a Rev property"
        }

        testCaseAsync "Finds docs with an expression" <| async {
            let expected = "finds_docs_with_expression"

            do! create {defaultRecord with Foo = expected} client |> Async.Ignore

            let! found = findByExpr <@ fun (c: MyTestClass) -> c.Foo = expected @> None client

            Expect.isNonEmpty found "findByExpr should have found at least one doc."
            Expect.all found (fun c -> c.Foo = expected) "All docs returned by findByExpr should have the expected Foo value."
        }

        testCaseAsync "Finds docs with a notequal expression" <| async {
            let expected = "finds_docs_with_a_notequal_expression"

            do! create ({defaultRecord with Foo = expected}) client |> Async.Ignore
            do! create defaultRecord client |> Async.Ignore

            let! found = findByExpr <@ fun (c: MyTestClass) -> c.Foo <> expected @> None client

            Expect.isNonEmpty found "findByExpr should have found at least one doc."
            Expect.all found (fun c -> c.Foo <> expected) "All docs returned should not have a Foo value equal to the search value."
        }

        testCaseAsync "Finds docs with a map" <| async {
            let expected = "finds_with_map"
            let record = { defaultRecord with Foo = expected }

            do! create record client |> Async.Ignore

            let map = Map.ofSeq ["Foo", [EqualTo expected]]
            let! found = findBySelector<MyTestClass> map None client

            Expect.isNonEmpty map "findByMap should have found at least one doc."
            Expect.all found (fun c -> c.Foo = expected) "All docs returned should have the expected Foo value."
        }

        testCaseAsync "Finds docs with a notequal map" <| async {
            let expected = "finds_docs_with_a_notequal_map"

            do! create ({defaultRecord with Foo = expected}) client |> Async.Ignore
            do! create defaultRecord client |> Async.Ignore

            let map = Map.ofSeq ["Foo", [NotEqualTo expected]]
            let! found = findBySelector<MyTestClass> map None client

            Expect.isNonEmpty map "findByMap should have found at least one doc."
            Expect.all found (fun c -> c.Foo <> expected) "All docs returned should not have a Foo value equal to the search value."
        }

        testCaseAsync "Doc exists" <| async {
            let! created = create defaultRecord client
            let! existsWithRev = exists created.Id (Some created.Rev) client
            let! existsWithoutRev = exists created.Id None client

            Expect.isTrue existsWithoutRev "Exists without rev should be true"
            Expect.isTrue existsWithRev "Exists with rev should be true"
        }

        testCaseAsync "Doc exists by expression" <| async {
            let expected = "doc_exists_by_expr"

            do! create ({defaultRecord with Foo = expected}) client |> Async.Ignore

            let! exists = existsByExpr <@ fun (c: MyTestClass) -> c.Foo = expected @> client

            Expect.isTrue exists ""
        }

        testCaseAsync "Doc exists by map" <| async {
            let expected = "doc_exists_by_map"

            do! create ({defaultRecord with Foo = expected}) client |> Async.Ignore

            let map = Map.ofSeq ["Foo", [EqualTo expected]]
            let! exists = existsBySelector map client

            Expect.isTrue exists ""
        }

        testCaseAsync "Copies docs" <| async {
            let uuid = sprintf "a-unique-string-%i" DateTime.UtcNow.Millisecond
            let! created = create defaultRecord client
            let! copy = copy created.Id uuid client

            Expect.isTrue copy.Ok ""
            Expect.equal copy.Id uuid "The copied document's id should have equaled the expected uuid."
        }

        testCaseAsync "Creates and deletes databases" <| async {
            let name = "davenport_fsharp_delete_me"
            let client = "localhost:5984" |> database name

            do! createDatabase client
            // Create the database again to ensure it doesn't fail if the database already existed
            do! createDatabase client

            let! deleteResponse =  deleteDatabase client

            Expect.isTrue deleteResponse.Ok "DeleteResponse.Ok should have been true."
        }

        testCaseAsync "Warnings work" <| async {
            let mutable called = false
            let handleWarning _ =
                called <- true

            let clientWithWarning = client |> warning (Event.add handleWarning)
            let! created = create defaultRecord clientWithWarning

            // To get a warning, attempt to delete a document without a revision. This will throw an error, but not before firing the warning event.
            do!
                clientWithWarning
                |> delete created.Id null
                |> Async.Catch
                |> Async.Ignore

            Expect.isTrue called "Warning event was not called"
        }

        testCaseAsync "Creates design docs and gets view results" <| async {
            // Make sure the design docs exist
            do! createDesignDocs defaultDesignDocs client

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

        ftestCaseAsync "Converts result to union type" <| async {
            let! firstDocResult = create<FirstDoc> ({ MyId = ""; MyRev = ""; hello = true }) client
            let! secondDocResult = create<SecondDoc> ({ MyId = ""; MyRev = ""; hello = 117 }) client

            let map (o: obj) =
                match o with
                | :? FirstDoc as x -> Doc1 x |> Some
                | :? SecondDoc as x -> Doc2 x |> Some
                | _ -> 
                    printfn "%A" o
                    None

            let! firstDoc = 
                get firstDocResult.Id (Some firstDocResult.Rev) client
                |> asyncMap (Option.bind map)
            
            Expect.isSome firstDoc "Result should not be None"
            Expect.isTrue (firstDoc |> function | Some (Doc1 _) -> true | _ -> false) "Result should be FirstDoc union type." 
            
            let firstDoc = 
                match firstDoc with 
                | Some (Doc1 x) -> x
                | _ -> failwith "Was not Some (Doc1 x)"

            Expect.isTrue (firstDoc.GetType() = typeof<FirstDoc>) "firstDoc type mismatch"
            Expect.isTrue firstDoc.hello "firstDoc.hello should equal true"
            Expect.isNotEmpty firstDoc.MyId "firstDoc.id should not be empty"
            Expect.isNotEmpty firstDoc.MyRev "firstDoc.rev should not be empty"
        }
    ]
