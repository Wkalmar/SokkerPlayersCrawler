open FSharp.Collections.ParallelSeq
open FSharp.Data
open System
open System.Text.RegularExpressions
open System.IO

type Player = HtmlProvider<"http://sokker.org/player/PID/25410835">
type Message = string * AsyncReplyChannel<string>

let minPlayerId = 29657066
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
    |> PSeq.withDegreeOfParallelism maxDop
    |> PSeq.iter(fun i -> 
        try
            let url = String.Format("http://sokker.org/player/PID/{0}", i)                        
            let player = Player.Load(url).Lists.List4
            let countryContainer = player.Values.[0]
            let priceContainer = player.Values.[1]            
            if countryContainer.Contains("Ukraina") 
            then 
                if extracrPrice priceContainer > minPrice
                then
                    let reply = fileWriterAgent.PostAndReply (fun replyChannel -> url, replyChannel)
                    ()
            else
                ()
        with
        | _ -> ()
    )

[<EntryPoint>]
let main argv = 
    load()
    0
