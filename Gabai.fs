namespace Gabai

open System
open System.Net
open System.IO

open Newtonsoft.Json.Linq

open Secrets


// ---------------------------------
// Gab.ai thumbnails
// ---------------------------------


module Thumbnail =
    open DataAccess
    open DataAccess.GabaiAccess

    let generateThumbnail name post =
        sprintf "QT_QPA_PLATFORM=offscreen phantomjs thumbnail.js https://gab.ai/%s/posts/%s %s/img/%s-%s.png" name post Globals.WebRoot name post
        |> Utils.execute

    let generateThumbnailFromDb () =
        "thumbnail_created_at is null"
        |> GabaiAccess.selectFromGab   
        |> List.iter (fun p -> 
            try
                (generateThumbnail p.ActuserName p.PostId) |> ignore
                let filePath = sprintf "%s/img/%s-%s.png" Globals.WebRoot p.ActuserName p.PostId
                if IO.File.Exists(filePath)  then
                    updatePost p updateCmdThumbnail
                else 
                    printfn "%s does not exist, %s, %s" filePath p.ActuserName p.PostId
            with _ as e -> printfn "%s" e.Message)
        ""

 

module Api =
    open DataAccess.Types
    open DataAccess.GabaiAccess
    open DataAccess.TwitterAccess
    open System.Text
    open Utils
    open System.Net.Sockets
    
    // ---------------------------------
    // Gab.ai API
    // ---------------------------------

    let loginUri = "https://gab.ai/auth/login"
    let feedUri name = sprintf "https://gab.ai/feed/%s" name
    let postUri = "https://gab.ai/posts"
    
    let rememberCookie = "remember_82e5d2c56bdd0811318f0cf078b78bfc"

    // ---------------------------------
    // Gab.ai functions
    // ---------------------------------
    
    let getToken (secret : Secret) =
        try            
            if Secrets.gabJwtToken.exp > Utils.UtcNow () 
            then
                Secrets.gabJwtToken
            else
                let jwtToken = JwtToken ()
                sprintf """php -f %s/phpGab.php username="%s" password="%s" """ 
                    Globals.ContentRoot (secret.gabUsername |> urlEncode) (secret.gabPassword |> urlEncode)
                |> Utils.execute
                |> JObject.Parse
                |> fun x -> 
                    printfn "x %s" (x.ToString())
                    let jwt = x.Item("std").ToString().Split([|'.'|])
                    if (jwt |> Array.map (fun s -> s.Length) |> fun x -> x=[|36;158;43|]) then
                        jwtToken.jwtHeader    <- jwt.[0] 
                        jwtToken.jwtPayload   <- jwt.[1] 
                        jwtToken.jwtSignature <- jwt.[2] 
                        jwtToken.exp          <- jwt.[1] |> Utils.getExpiryDate
                    else 
                        printf """{ "std": "%s", "err": "%s", "error_msg": "JWT token validation failed" }""" 
                            (x.Item("std").ToString()) (x.Item("err").ToString())
                    Secrets.gabJwtToken <- jwtToken
                    updateGabJwtToken jwtToken
                    jwtToken
        with 
        | :? WebException as e -> 
            let jwtToken = JwtToken()
            jwtToken.jwtPayload <- 
                sprintf """{ "error_msg": {"headers":"%s", "message":"%s","status":"%A"} }""" 
                    (seq{ for h in e.Response.Headers do yield sprintf "%s" h} 
                    |> Seq.fold (fun s t -> if s = "" then t else sprintf "%s,\n%s" s t) "" 
                    |> sprintf """{ "msg": [%s] }""" ) e.Message e.Status
            jwtToken
        | _ as e -> 
            let jwtToken = JwtToken()
            jwtToken.jwtPayload <- sprintf """{ "error_msg": "%s" }""" e.Message
            jwtToken


    let getFeed (secret : Secret) user = 
        try
            let jwt = getToken secret
            let req  = HttpWebRequest.Create((feedUri user), Method="GET")
            req.Headers.Clear()
            req.Headers.Set("User-Agent", Globals.UserAgent)
            req.Headers.Set("Accept", "application/json")
            req.Headers.Set("Authorization", (sprintf "Bearer %s.%s.%s" jwt.jwtHeader jwt.jwtPayload jwt.jwtSignature))
            req.Headers.Set("Cookie", rememberCookie)
            use resp = req.GetResponse()
            use strm = new StreamReader(resp.GetResponseStream())
            strm.ReadToEnd()
        with 
        | :? WebException as e -> printfn "headers %A message %s status %A" e.Response.Headers e.Message e.Status; """{ "data": [] }"""
        | _ as e -> printfn "%s" e.Message; """{ "data": [] }"""


    /// feed / data / actuser / username  -> actuser_name
    /// feed / data / id                  -> post_id
    /// feed / data / post / body         -> post_body
    /// feed / data / post / created_at   -> post_created_at
    let insertFeed (secret : Secret) user = 
        try
            let feed = 
                user
                |> getFeed secret
                |> JObject.Parse
            feed.Item("data")
            |> Seq.map (fun d -> 
                let p = PostRecord ()
                p.ActuserName   <- d.Item("actuser").Item("username").ToString() 
                p.PostId        <- d.Item("post").Item("id").ToString()
                p.PostBody      <- d.Item("post").Item("body").ToString()
                p.PostCreatedAt <- d.Item("post").Item("created_at").ToString()
                p)
            |> addPostSeq
            sprintf """{ "user": "%s", "records": %d }""" user (feed.Item("data") |> Seq.length)
        with _ as e -> sprintf """{ "user": "%s", "error_msg": "%s" }""" user e.Message


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
            |> addPostSeq
            "offline records written" 
        with _ as e -> e.Message
          

    let postGab (secret : Secret) (post : GabPost) = 
        try
            let data = (post |> GabPostEncoded).ToCharArray() |> Array.map byte
            let req = HttpWebRequest.Create(postUri) :?> HttpWebRequest
            let jwt = getToken secret
            req.ProtocolVersion <- HttpVersion.Version11
            req.Headers.Clear()
            req.Timeout <- 1 * 60 * 1000
            req.Method <- "POST"
            req.UserAgent <- Globals.UserAgent
            req.ContentType <- "application/json"
            req.ContentLength <- int64 data.Length
            req.Headers.Set("Authorization", 
                            (sprintf "Bearer %s.%s.%s" 
                                jwt.jwtHeader jwt.jwtPayload jwt.jwtSignature))
            req.Headers.Set("Connection", "close")
            use wstream = req.GetRequestStream() 
            wstream.Write(data,0, (data.Length))
            wstream.Close()
            use resp = req.GetResponse()
            use strm = new StreamReader(resp.GetResponseStream())
            strm.ReadToEnd()
        with 
        | :? WebException as e -> 
            sprintf """{ "error_msg": {"headers":"%A", "message":"%s","status":"%A"} }""" 
                e.Response.Headers e.Message e.Status; 
        | _ as e -> sprintf """{ "error_msg": "%s" }""" e.Message


    let postTweetOnGab (secret : Secret) (feedRecord : FeedRecord) = 
        let gabPost = GabPost()
        gabPost.body <- feedRecord.Status
        let result = postGab secret gabPost
        if result.StartsWith ("""{ "error_msg":""") then 
            result
        else
            updateUserChunkFeedRecord feedRecord
        

    let postTwitterLeadersOnGab (secret : Secret)  = 
        try
            selectTweetsFeedChunk () 
            |> Seq.map (postTweetOnGab secret)
            |> Seq.fold (fun s t -> if s = "" then t else sprintf "%s,\n%s" s t) ""
            |> sprintf """{ "msg": [%s] }"""
        with _ as e -> sprintf """{ "error_msg": "%s" }""" e.Message

