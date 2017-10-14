module Json

open System
open System.Runtime.Serialization.Json


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
