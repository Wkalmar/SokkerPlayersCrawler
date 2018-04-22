open Akka.Actor
open Akka.Streams
open Akka.Streams.Supervision
open FSharp.Collections.ParallelSeq
open FSharp.Data
open System
open System.Text.RegularExpressions
open System.IO
open Akkling.Streams

type Player = HtmlProvider<"http://sokker.org/player/PID/25410835">

let minPlayerId = 29641529
let maxPlayerId = 29657066
let minPrice = 1391000
let priceRegex = Regex("[0-9 ]{1,}")
let fileName = "output.txt"
let maxDop = 4
    
let extractPrice input =
    match priceRegex.Match input with
    | m when m.Success ->
        let value = m.Value
        Regex.Replace(value, @"\s+", "") |> int            
    | _ -> 0

let system = ActorSystem.Create("test")

let mat = 
    ActorMaterializer.Create(
        system, 
        ActorMaterializerSettings.Create(system).WithSupervisionStrategy(Deciders.ResumingDecider))
    
let load() = 
    [minPlayerId..maxPlayerId]
    |> Source.ofList
    // no more than maxDop of the following async will run at any time
    |> Source.asyncMapUnordered maxDop (fun i ->
        async {
            try
                let url = sprintf "http://sokker.org/player/PID/%d" i
                let! res = Player.AsyncLoad url                        
                let player = res.Lists.List4
                let countryContainer = player.Values.[0]
                let priceContainer = player.Values.[1]
                return 
                    if not (countryContainer.Contains "Ukraina") then Error (url, "no Ukraina")
                    elif extractPrice priceContainer < minPrice then Error (url, "too cheap")
                    else Ok url
            with e ->
                return Error ("", e.Message)
        })
    // I use Result above because it's prohibited to use Option in stream because None is presented as null
    // at runtime and Akka.Streams fails.
    |> Source.choose (function 
        | Ok url -> Some url 
        | Error e ->
            printfn "Error: %A" e 
            None)
    // the whole stream will block if we cannot write into the file fast enough (with 16 element buffer by default)
    |> Source.runForEach mat (fun url ->
        printfn "Writing %s..." url 
        File.AppendAllLines(fileName, [url]))

[<EntryPoint>]
let main argv =
    load() |> Async.RunSynchronously // wait for the whole stream to finish
    0
