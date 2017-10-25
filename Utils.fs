module Utils

open System
open System.Text
open System.Threading
open System.Runtime.Serialization.Json
open System.Security.Cryptography

let random = Random()

let swap (a: _[]) x y =
    let tmp = a.[x]
    a.[x] <- a.[y]
    a.[y] <- tmp

// shuffle an array (in-place)
let shuffle a =
    Array.iteri (fun i _ -> swap a i (random.Next(i, Array.length a))) a

let unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
let urlEncode str =
    String.init (String.length str) (fun i ->
        let symbol = str.[i]
        if unreservedChars.IndexOf(symbol) = -1 then
            "%" + String.Format("{0:X2}", int symbol)
        else
            string symbol)

let urlDecode = System.Web.HttpUtility.UrlDecode 
let htmlEncode = System.Web.HttpUtility.HtmlEncode
let htmlDecode = System.Web.HttpUtility.HtmlDecode
let javascriptEncode = System.Web.HttpUtility.JavaScriptStringEncode

let twurl args =
    use proc = new System.Diagnostics.Process()

    proc.StartInfo.FileName <- "/usr/local/bin/twurl"
    proc.StartInfo.Arguments <- args
    proc.StartInfo.UseShellExecute <- false
    proc.StartInfo.RedirectStandardOutput <- true
    proc.StartInfo.RedirectStandardError <- true
    proc.Start() |> ignore
   
    proc.WaitForExit()
    sprintf "{ \"std\": \"%s\", \"err\": \"%s\" }" (proc.StandardOutput.ReadToEnd() |> urlEncode)  (proc.StandardError.ReadToEnd() |> urlEncode)


let execute command =
    use proc = new System.Diagnostics.Process()

    proc.StartInfo.FileName <- "/bin/bash"
    proc.StartInfo.Arguments <- "-c \" " + command + " \""
    proc.StartInfo.UseShellExecute <- false
    proc.StartInfo.RedirectStandardOutput <- true
    proc.StartInfo.RedirectStandardError <- true
    proc.Start() |> ignore
   
    proc.WaitForExit()
    sprintf "{ \"std\": \"%s\", \"err\": \"%s\" }" (proc.StandardOutput.ReadToEnd() |> urlEncode)  (proc.StandardError.ReadToEnd() |> urlEncode)

let wait sec eps =
    sec * 1000. * (1. + random.NextDouble() * eps * (if random.Next() % 2 = 0 then 1. else -1.))
    |> int |> Thread.Sleep

// ---------------------------------
// Core algorithms
// ---------------------------------

let hmacsha1 signingKey str =
    use converter = new HMACSHA1(Encoding.ASCII.GetBytes(signingKey : string))
    Encoding.ASCII.GetBytes(str : string)
    |> converter.ComputeHash
    |> Convert.ToBase64String

let compositeSigningKey consumerSecret tokenSecret =
    urlEncode(consumerSecret) + "&" + urlEncode(tokenSecret)

let baseString httpMethod baseUri queryParameters =
    queryParameters
    |> Seq.sortBy (fun (k,_) -> k)
    |> Seq.map (fun (k,v) -> urlEncode(k) + "%3D" + urlEncode(v))
    |> String.concat "%26"
    |> sprintf "%s&%s&%s" httpMethod (urlEncode(baseUri))

let createAuthorizeHeader queryParameters =
    queryParameters
    |> Seq.map (fun (k,v) -> urlEncode(k)+"\x3D\""+urlEncode(v)+"\"")
    |> String.concat ","
    |> sprintf "OAuth %s"

let currentUnixTime() =
    (DateTime.UtcNow - DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds
    |> floor
    |> int64
    |> sprintf "%d"


// ---------------------------------
// JSON utilities
// ---------------------------------

let toString = System.Text.Encoding.ASCII.GetString
let toBytes (x : string) = System.Text.Encoding.ASCII.GetBytes x

let SerializeJson<'a> (x : 'a) = 
    let jsonSerializer = DataContractJsonSerializer(typedefof<'a>)
    use stream = new IO.MemoryStream()
    jsonSerializer.WriteObject(stream, x)
    toString <| stream.ToArray()

let DeserializeJson<'a> (json : string) =
    let jsonSerializer = DataContractJsonSerializer(typedefof<'a>)
    use stream = new IO.MemoryStream(toBytes json)
    jsonSerializer.ReadObject(stream) :?> 'a

