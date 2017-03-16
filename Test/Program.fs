open FsCheck

open CardCatalog.Test.Generators

[<EntryPoint>]
let main argv =
    printfn "Press any key to close..."
    System.Console.ReadKey () |> ignore

    0 // return an integer exit code
