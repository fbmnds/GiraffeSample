namespace Gabai

open System
open System.Net
open System.IO
open System.IO.Compression

open Newtonsoft.Json.Linq

open Secrets
open GabaiTypes
open DataAccess



// ---------------------------------
// Gab.ai thumbnails
// ---------------------------------


module Thumbnail =
    open DataAccess.GabaiAccess

    let execute name post =
        let command = sprintf "QT_QPA_PLATFORM=offscreen phantomjs thumbnail.js https://gab.ai/%s/posts/%s %s/img/%s-%s.png" name post Globals.WebRoot name post
        use proc = new System.Diagnostics.Process()

        proc.StartInfo.FileName <- "/bin/bash"
        proc.StartInfo.Arguments <- "-c \" " + command + " \""
        proc.StartInfo.UseShellExecute <- false
        proc.StartInfo.RedirectStandardOutput <- true
        proc.StartInfo.RedirectStandardError <- true
        proc.Start() |> ignore
   
        proc.WaitForExit()
        proc.StandardOutput.ReadToEnd(), proc.StandardError.ReadToEnd()



    let executeThumbnailFromDb () =
        "thumbnail_created_at is null"
        |> GabaiAccess.selectFromGab   
        |> List.iter (fun p -> 
            try
                (execute p.ActuserName p.PostId) |> ignore
                let filePath = sprintf "%s/projects/GiraffeSampleApp/WebRoot/img/%s-%s.png" (Environment.GetEnvironmentVariable("HOME")) p.ActuserName p.PostId
                if IO.File.Exists(filePath)  then
                    updatePost p updateCmdThumbnail
                else printfn "%s does not exist, %s, %s" filePath p.ActuserName p.PostId
            with _ as e -> printfn "%s" e.Message)
        ""

 

module Api =
    
    // ---------------------------------
    // Gab.ai API
    // ---------------------------------

    let loginUri = "https://gab.ai/auth/login"
    let feedUri name = sprintf "https://gab.ai/feed/%s" name

    let rememberCookie = "remember_82e5d2c56bdd0811318f0cf078b78bfc"
    let pattern  = """<input type="hidden" name="_token" value="""
    let position = 5

    // ---------------------------------
    // Gab.ai functions
    // ---------------------------------

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
        let data = sprintf "_token=%s&username=%s&password=%s&remember=on" token secret.gabUsername (secret.gabPassword |> Utils.urlEncode) 
        req.Headers.Clear()
        req.Headers.Set("User-Agent", Globals.UserAgent)
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
            req.Headers.Set("User-Agent", Globals.UserAgent)
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
            req.Headers.Set("User-Agent", Globals.UserAgent)
            req.Headers.Set("Accept", "application/json")
            req.Headers.Set("Authorization", (sprintf "Bearer %s.%s.%s" secret.gabJwtHeader secret.gabJwtPayload secret.gabJwtSignature))
            req.Headers.Set("Cookie", rememberCookie)
            use resp = req.GetResponse()
            use strm = new StreamReader(resp.GetResponseStream())
            strm.ReadToEnd()
        with 
        | :? WebException as e -> printfn "headers %A message %s status %A" e.Response.Headers e.Message e.Status; "{ \"feed\": [] }"
        | _ as e -> printfn "%s" e.Message; "{ \"data\": [] }"


    /// feed / data / actuser / username  -> actuser_name
    /// feed / data / id                  -> post_id
    /// feed / data / post / body         -> post_body
    /// feed / data / post / created_at   -> post_created_at
    let insertFeed user = 
        try
            let feed = 
                user
                |> getFeed
                |> JObject.Parse
            feed.Item("data")
            |> Seq.map (fun d -> 
                let p = PostRecord ()
                p.ActuserName   <- d.Item("actuser").Item("username").ToString() 
                p.PostId        <- d.Item("post").Item("id").ToString()
                p.PostBody      <- d.Item("post").Item("body").ToString()
                p.PostCreatedAt <- d.Item("post").Item("created_at").ToString()
                p)
            |> GabaiAccess.addPostSeq
            sprintf "%s feed: %d records written to database" user (feed.Item("data") |> Seq.length)
        with _ as e -> e.Message


    let offlineTweetFeed _ =
        try
            let feed = 
                "samples/gab.json" 
                |> File.ReadAllText
                |> JObject.Parse
            feed.Item("feed").Item("data")
            |> Seq.map (fun d -> 
                let p = PostRecord ()
                p.ActuserName   <- d.Item("actuser").Item("username").ToString() 
                p.PostId        <- d.Item("post").Item("id").ToString()
                p.PostBody      <- d.Item("post").Item("body").ToString()
                p.PostCreatedAt <- d.Item("post").Item("created_at").ToString()
                p)
            |> GabaiAccess.addPostSeq
            "offline records written" 
        with _ as e -> e.Message
          
        