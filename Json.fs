module Json

open System
open System.Text
open System.Runtime.Serialization.Json
open System.Security.Cryptography

let unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
let urlEncode str =
    String.init (String.length str) (fun i ->
        let symbol = str.[i]
        if unreservedChars.IndexOf(symbol) = -1 then
            "%" + String.Format("{0:X2}", int symbol)
        else
            string symbol)

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

