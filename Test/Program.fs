(* Any copyright is dedicated to the Public Domain.
 * http://creativecommons.org/publicdomain/zero/1.0/
 *)

open FsCheck

open CardCatalog.Test.Generators

[<EntryPoint>]
let main argv =
    printfn "Press any key to close..."
    System.Console.ReadKey () |> ignore

    0 // return an integer exit code
