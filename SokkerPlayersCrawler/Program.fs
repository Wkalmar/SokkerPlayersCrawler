open FSharp.Data
open System
open System.Text.RegularExpressions
open System.IO

type Player = HtmlProvider<"http://sokker.org/player/PID/25410835">
type Message = string * AsyncReplyChannel<string>

let minPlayerId = 35657066
let maxPlayerId = 29641529
let minPrice = 1391000
let priceRegex = Regex("[0-9 ]{1,}")
let fileName = "output.txt"
let maxDop = 4
    
let extracrPrice input =
    let matched = priceRegex.Match input
    if matched.Success
    then
        let value = matched.Value
        Regex.Replace(value, @"\s+", "") |> int            
    else
        0

let fileWriterAgent = MailboxProcessor<Message>.Start(fun inbox ->
    let rec messageLoop() = async {
        let! (url, replyChannel) = inbox.Receive()
        File.AppendAllLines(fileName, [url])
        replyChannel.Reply("recieved")
        return! messageLoop()
    }
    messageLoop()    
)
    
let load() = 
    [|maxPlayerId..minPlayerId|]    
    |> Array.Parallel.iter(fun i -> 
        try
            let url = String.Format("http://sokker.org/player/PID/{0}", i)                        
            let player = Player.Load(url).Lists.List4
            let countryContainer = player.Values.[0]
            let priceContainer = player.Values.[1]            
            if countryContainer.Contains("Ukraina") 
            then 
                if extracrPrice priceContainer > minPrice
                then
                    fileWriterAgent.PostAndReply (fun replyChannel -> url, replyChannel) |> ignore
                    ()
            else
                ()
        with
        | ex -> 
            match ex.Message with
            | "Invalid HTML" -> ()
            | _ -> fileWriterAgent.PostAndReply (fun replyChannel -> ex.Message, replyChannel) |> ignore
    )

[<EntryPoint>]
let main argv = 
    load()
    0
