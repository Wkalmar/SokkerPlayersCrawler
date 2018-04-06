open FSharp.Data
open System
open System.Text.RegularExpressions
open System.IO

type Player = HtmlProvider<"http://sokker.org/player/PID/25410835">

let minPlayerId = 29657066
let maxPlayerId = 29641529
let minPrice = 1391000
let priceRegex = Regex("[0-9 ]{1,}")
let fileName = "output.txt"
    
let extracrPrice input =
    let matched = priceRegex.Match input
    if matched.Success
    then
        let value = matched.Value
        Regex.Replace(value, @"\s+", "") |> int            
    else
        0
   
let load() = 
    [|maxPlayerId..minPlayerId|]
    |> Array.iter(fun i -> 
        try
            let url = String.Format("http://sokker.org/player/PID/{0}", i)                        
            let player = Player.Load(url).Lists.List4
            let countryContainer = player.Values.[0]
            let priceContainer = player.Values.[1]            
            if countryContainer.Contains("Ukraina") 
            then 
                if extracrPrice priceContainer > minPrice
                then
                    File.AppendAllLines(fileName, [url])
            else
                ()
        with
        | _ -> ()
    )

[<EntryPoint>]
let main argv = 
    load()
    0
