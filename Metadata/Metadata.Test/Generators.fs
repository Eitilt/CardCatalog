module Generators

open System.IO
open FsCheck

type ReadSeekWriteStream (bytes) =
    inherit MemoryStream (bytes, true)
    let buffer = bytes

    override stream.ToString () =
        sprintf "read, seek, write : %A" buffer


    static member Generate =
        Gen.apply
        <| gen {
                return fun bs -> new ReadSeekWriteStream (bs) :> Stream
            }
        <| Gen.sized (fun len -> Gen.arrayOfLength len Arb.generate<byte>)

type ReadSeekStream (bytes) =
    inherit MemoryStream (bytes, false)
    let buffer = bytes

    override stream.ToString () =
        sprintf "read, seek, _____ : %A" buffer


    static member Generate =
        Gen.apply
        <| gen {
                return fun bs -> new ReadSeekStream (bs) :> Stream
            }
        <| Gen.sized (fun len -> Gen.arrayOfLength len Arb.generate<byte>)

type Stream with
    static member ArbInstance () =
        [ ReadSeekWriteStream.Generate
        ; ReadSeekStream.Generate
        ]
        |> Gen.oneof
        |> Arb.fromGen
Arb.register<Stream> () |> ignore

type ReadableStream =
    static member ArbInstance () =
        [ ReadSeekWriteStream.Generate
        ; ReadSeekStream.Generate
        ]
        |> Gen.oneof
        |> Arb.fromGen
Arb.register<ReadableStream> () |> ignore
