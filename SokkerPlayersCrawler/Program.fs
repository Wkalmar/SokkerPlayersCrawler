open Akka.Actor
open Akka.Streams
open Akka.Streams.Supervision
open FSharp.Data
open System
open System.Collections.Generic
open System.Text.RegularExpressions
open System.IO
open Akkling.Streams

type Player = HtmlProvider<"http://sokker.org/player/PID/36363863">

let minPlayerId = 36406546
let maxPlayerId = 37227362
let minPrice = 136600
let maxAge = 19
let priceRegex = Regex("[0-9 ]{1,}")
let fileName = "output.txt"
let maxDop = 4
    
let extractPrice input =
    match priceRegex.Match input with
    | m when m.Success ->
        let value = m.Value
        Regex.Replace(value, @"\s+", "") |> int            
    | _ -> 0

let tryParse = Int32.TryParse >> function
    | true, v    -> Some v
    | false, _   -> None

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
                try
                    let _ = res.Tables.Table2
                    return Error (url, "already in NT")
                with 
                | :? KeyNotFoundException -> 
                    let ageString = (Array.ofList (res.Html.CssSelect(".panel-heading>.title-block-1>strong"))).[0].DirectInnerText()
                    let age = tryParse ageString
                    match age with
                    | Some a -> 
                        if a > maxAge then return Error(url, "too old")
                        else
                            let player = res.Lists.List4                
                            let countryContainer = player.Values.[0]
                            let priceContainer = player.Values.[1]                    
                            return 
                                if not (countryContainer.Contains "Ukraina") then Error (url, "no Ukraina")
                                elif extractPrice priceContainer < minPrice then Error (url, "too cheap")
                                else Ok url
                    | None -> return Error(url, "Invalid html")
                    
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
