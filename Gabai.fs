module Gabai

open System
open System.Net
open System.IO

open Newtonsoft.Json.Linq
open System.IO.Compression

let loginUri = "https://gab.ai/auth/login"
let feedUri name = sprintf "https://gab.ai/feed/%s" name
let rememberCookie = "remember_82e5d2c56bdd0811318f0cf078b78bfc"
let pattern  = """<input type="hidden" name="_token" value="""
let position = 5

let unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
let urlEncode str =
    String.init (String.length str) (fun i ->
        let symbol = str.[i]
        if unreservedChars.IndexOf(symbol) = -1 then
            "%" + String.Format("{0:X2}", int symbol)
        else
            string symbol)

let gabUsername, gabPassword, gabJwtHeader, gabJwtPayload, gabJwtSignature =
    let s = 
        let home = Environment.GetEnvironmentVariable("HOME")
        if home.Contains(@":\") then home + @"\.ssh\secret.json" else home + @"/.ssh/secret.json"
        |> File.ReadAllText
        |> JObject.Parse
    s.Item("gabUsername").ToString(), 
    s.Item("gabPassword").ToString(),
    s.Item("gabJwtHeader").ToString(),
    s.Item("gabJwtPayload").ToString(),
    s.Item("gabJwtSignature").ToString()


let deleteLines linesToRemove (text : string) = 
    text.Split(Environment.NewLine.ToCharArray(), linesToRemove + 1)
    |> Seq.tail
    |> String.concat ""

let getToken () = 
    let req  = WebRequest.Create(loginUri, Method="GET")
    use resp = req.GetResponse()
    use strm = new StreamReader(resp.GetResponseStream())
    strm.ReadToEnd().Split(Environment.NewLine.ToCharArray())
    |> Array.filter (fun s -> s.Contains(pattern))
    |> String.concat ""
    |> fun t -> t.Trim([|' ';'<';'>';'"'|]).Split([|'"'|]).[position]


// WORKAROUND use php script for retrieving the JWT token
let getToken2 () =
    let token = getToken ()
    //let req  = HttpWebRequest.Create("http://192.168.0.39", Method="POST")
    let req  = WebRequest.Create(loginUri, Method="POST") 
    let data = sprintf "_token=%s&username=%s&password=%s&remember=on" token gabUsername (gabPassword |> urlEncode) 
    req.Headers.Clear()
    req.Headers.Set("User-Agent", "phpGab/1.0")
    req.Headers.Set("Accept", "*/*")
    req.Headers.Set("Connection", "close")
    req.Headers.Set("Cookie", "foo=bar")
    req.Headers.Set("Accept-Encoding", "gzip, deflate")
    req.Headers.Set("Content-Type", "application/x-www-form-urlencoded")
    req.ContentLength <- (int64) data.Length
    use wstream = req.GetRequestStream() 
    wstream.Write((data.ToCharArray() |> Array.map (byte)),0, (data.Length))
    wstream.Flush()
    //wstream.Close()
    //req.Timeout <- 5000 
    try
        use resp = req.GetResponse()
        use strm = new StreamReader(resp.GetResponseStream())
        strm.ReadToEnd()
    with 
    | :? WebException as e -> (sprintf "headers %A message %s status %A" e.Response.Headers e.Message e.Status)
    | _ as e -> e.Message


let getFeed2 user : string = 
    try
        let req  = HttpWebRequest.Create((feedUri user), Method="GET")
        req.Headers.Clear()
        req.Headers.Set("User-Agent", "phpGab/1.0")
        req.Headers.Set("Accept", "*/*")
        req.Headers.Set("Connection", "close")
        req.Headers.Set("Cookie", rememberCookie)
        //req.Headers.Set("Accept-Encoding", "gzip, deflate")
        
        use resp = req.GetResponse() :?> HttpWebResponse
        let buffer = Array.init 30000 (fun _ -> 30uy)
        let strm =
            match resp.ContentEncoding.ToUpperInvariant() with
            | "GZIP" -> 
                let strm = new GZipStream(resp.GetResponseStream(), CompressionMode.Decompress)
                strm.BaseStream
            | "DEFLATE" -> 
                let strm = new DeflateStream(resp.GetResponseStream(), CompressionMode.Decompress)
                strm.BaseStream
            | _ -> 
                let strm = resp.GetResponseStream()
                strm
 //       use strm = new StreamReader(resp.GetResponseStream())
        //strm.ReadTimeout <- 5000
        let upper = strm.Read(buffer, 0, 30000) - 1
        if upper > -1 then
            buffer.[0 .. upper] |> Array.map (char) |> Array.map (string) |> String.concat ""
        else "empty buffer" 
    with 
    | :? WebException as e -> (sprintf "headers %A message %s status %A" e.Response.Headers e.Message e.Status)
    | _ as e -> e.Message

let getFeed user = 
    try
        let req  = HttpWebRequest.Create((feedUri user), Method="GET")
        req.Headers.Clear()
        req.Headers.Set("User-Agent", "phpGab/1.0")
        req.Headers.Set("Accept", "application/json")
        req.Headers.Set("Authorization", (sprintf "Bearer %s.%s.%s" gabJwtHeader gabJwtPayload gabJwtSignature))
        req.Headers.Set("Cookie", rememberCookie)
        use resp = req.GetResponse()
        use strm = new StreamReader(resp.GetResponseStream())
        sprintf "{ \"feed\": [%s] }" (strm.ReadToEnd())
    with 
    | :? WebException as e -> printfn "headers %A message %s status %A" e.Response.Headers e.Message e.Status; "{ \"feed\": [] }"
    | _ as e -> printfn "%s" e.Message; "{ \"feed\": [] }"