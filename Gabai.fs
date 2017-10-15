module Gabai

open System
open System.Net
open System.IO
open System.Xml.XPath

let loginUri = "https://gab.ai/auth/login"

let deleteLines linesToRemove (text : string) = 
    text.Split(Environment.NewLine.ToCharArray(), linesToRemove + 1)
    |> Seq.tail
    |> String.concat ""


let pattern = """<input type="hidden" name="_token" value="""
let position = 5

let getToken () = 
    let req  = WebRequest.Create(loginUri, Method="GET")
    use resp = req.GetResponse()
    use strm = new StreamReader(resp.GetResponseStream())
    strm.ReadToEnd().Split(Environment.NewLine.ToCharArray())
    |> Array.filter (fun s -> s.Contains(pattern))
    |> String.concat ""
    |> fun t -> t.Trim([|' ';'<';'>';'"'|]).Split([|'"'|]).[position]
