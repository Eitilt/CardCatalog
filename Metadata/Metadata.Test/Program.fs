open FsCheck

open Generators

[<EntryPoint>]
let main argv =
    Gen.sample 5 5 Arb.generate<ReadableStream>
    |> printfn "%A"
    
    printfn "Press any key to close..."
    System.Console.ReadKey () |> ignore

    0 // return an integer exit code
