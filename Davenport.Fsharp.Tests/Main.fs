module Davenport.Fsharp.Tests
open Expecto

[<EntryPoint>]
let main argv =
    // Set debug to `true` below to start debugging.
    // 1. Start the test suite (dotnet run)
    // 2. Go to VS Code's Debug tab.
    // 3. Choose ".NET Core Attach"
    // 4. Choose one of the running processes. It's probably the one that says 'dotnet exec yadayada path to app'. Several processes may start and stop while the project is building.
    let debug = false
    if debug then
        printfn "Waiting to attach debugger. Run .NET Core Attach under VS Code's debug menu."
        
        while not(System.Diagnostics.Debugger.IsAttached) do
            System.Threading.Thread.Sleep(100)

        System.Diagnostics.Debugger.Break()

    Tests.runTestsInAssembly defaultConfig argv
