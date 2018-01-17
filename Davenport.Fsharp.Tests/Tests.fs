module Tests

open System
open Expecto
open Davenport.Fsharp.Wrapper

type MyTestClass = {
    MyId: string
    MyRev: string
    Foo: string
    Bar: bool
    Baz: int
}

let defaultRecord = {
    MyId = ""
    MyRev = ""
    Foo = "test value"
    Bar = true
    Baz = 11
}

let client =
    "localhost:5984"
    |> database "davenport_net_fsharp"
    |> idName "MyId"
    |> revName "MyRev"


let notNullOrEmpty (s: string) = String.IsNullOrEmpty s |> Expect.isFalse

let asyncMap (fn: 'a -> 'b) (task: Async<'a>) = async {
    let! result = task

    return fn result
}

[<Tests>]
let tests =
    printfn "Configuring database."

    // Configure the database before running tests
    configureDatabase [] [] client
    |> Async.RunSynchronously

    printfn "Database configured."

    // Set `false` to `true` below to start debugging.
    // 1. Start the test suite (dotnet run)
    // 2. Go to VS Code's Debug tab.
    // 3. Choose ".NET Core Attach"
    // 4. Choose one of the running processes. It's probably the one that says 'dotnet exec yadayada path to app'. Several processes may start and stop while the project is building.
    if true then
        printfn "Waiting to attach debugger. Run .NET Core Attach under VS Code's debug menu."
        while not(System.Diagnostics.Debugger.IsAttached) do
          System.Threading.Thread.Sleep(100)
        System.Diagnostics.Debugger.Break()

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

            let map = Map.ofSeq ["Foo", EqualTo expected]

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

        ftestCaseAsync "Lists with docs" <| async {
            // Create at least one doc to list
            do! create defaultRecord client |> Async.Ignore

            let! list = listWithDocs<MyTestClass> None client

            Expect.equal list.Offset 0 "List offset is not 0."
            Expect.isNonEmpty list.Rows "List is empty."
            Expect.all list.Rows (fun r -> r.Doc.GetType() <> typeof<MyTestClass>) "All docs should be of type MyTestClass"
            Expect.all list.Rows (fun r -> not <| r.Id.StartsWith "_design") "No doc should start with _design"
            Expect.all list.Rows (fun r -> not <| String.IsNullOrEmpty r.Doc.MyId) "No doc should have an empty id"
            Expect.all list.Rows (fun r -> not <| String.IsNullOrEmpty r.Doc.MyRev) "No doc should have an empty rev"
        }
    ]