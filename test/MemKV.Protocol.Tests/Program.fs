open Expecto

open MemKV.Protocol.Tests

[<Tests>]
let tests = 
    testList "All" [
        MessageTests.tests
    ]

[<EntryPoint>]
let main argv =
    printfn "Running tests!"   
    runTestsWithCLIArgs [] argv tests