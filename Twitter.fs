module Twitter

open System
open System.IO
open System.Net

open System.Text

open Newtonsoft.Json.Linq

open Secrets
open Utils
open DataAccess.GabaiAccess
open DataAccess.Types


// ---------------------------------
// Twitter API
// ---------------------------------

let requestTokenURI      = "https://api.twitter.com/oauth/request_token"
let accessTokenURI       = "https://api.twitter.com/oauth/access_token"
let authorizeURI         = "https://api.twitter.com/oauth/authorize"
let verifyCredentialsURI = "https://api.twitter.com/1.1/account/verify_credentials.json"
let searchURI            = "https://api.twitter.com/1.1/statuses/user_timeline.json" 
let homeTimelineURI      = "https://api.twitter.com/1.1/statuses/home_timeline.json"
let statusURI            = "https://api.twitter.com/1.1/statuses/update.json"


//[<DataContract(Name="repo")>]
//[<CLIMutable>]
type Post =
    { 
        status                : string
        in_reply_to_status_id : Option<string>
        possibly_sensitive    : Option<bool> 
        lat                   : Option<float>
        long                  : Option<float>
        place_id              : Option<string>
        display_coordinates   : Option<bool>
        trim_user             : Option<bool>
        media_ids             : Option<string>
        enable_dm_commands    : Option<bool>
        fail_dm_commands      : Option<bool> 
    }


let oauthParameters consumerKey accessToken =
    [
        "oauth_version",          "1.0"
        "oauth_consumer_key",     consumerKey
        "oauth_nonce",            System.Guid.NewGuid().ToString().Substring(24)
        "oauth_signature_method", "HMAC-SHA1"
        "oauth_timestamp",        currentUnixTime()
        "oauth_token",            accessToken 
    ]

let oauthRedirectParameters consumerKey =
    [
        "oauth_callback",         "oob"
        "oauth_consumer_key",     consumerKey
        "oauth_nonce",            System.Guid.NewGuid().ToString().Substring(24)
        "oauth_signature_method", "HMAC-SHA1"
        "oauth_timestamp",        currentUnixTime()
        "oauth_version",          "1.0"
    ]

/// Request a token from Twitter and return:
/// oauth_token, oauth_token_secret, oauth_callback_confirmed
let requestOAuthToken () =
    let queryParameters       = oauthRedirectParameters secret.consumerKey
    let signingString         = baseString "POST" requestTokenURI queryParameters
    let signingKey            = compositeSigningKey secret.consumerSecret ""
    let oauth_signature       = hmacsha1 signingKey signingString
    let signedQueryParameters = ("oauth_signature", oauth_signature) :: queryParameters
    let req                   = WebRequest.Create(requestTokenURI, Method="POST")
    let headerValue           = createAuthorizeHeader signedQueryParameters
    req.Headers.Add(HttpRequestHeader.Authorization, headerValue)

    use resp   = req.GetResponse()
    use strm  = new StreamReader(resp.GetResponseStream())
    let parts  = strm.ReadToEnd().Split('&')
    (parts.[0].Split('=').[1],
     parts.[1].Split('=').[1],
     parts.[2].Split('=').[1] = "true")


/// Get an access token and access token secret
let accessToken token tokenSecret verifier =
    let queryParameters     = ("oauth_verifier",verifier) :: (oauthParameters secret.consumerKey token)
    let signingString       = baseString "POST" accessTokenURI queryParameters
    let signingKey          = compositeSigningKey secret.consumerSecret tokenSecret
    let oauth_signature     = hmacsha1 signingKey signingString
    let realQueryParameters = ("oauth_signature", oauth_signature) :: queryParameters
    let req                 = WebRequest.Create(accessTokenURI, Method="POST")
    let headerValue         = createAuthorizeHeader realQueryParameters
    req.Headers.Add(HttpRequestHeader.Authorization, headerValue)

    use resp   = req.GetResponse()
    use stream = new StreamReader(resp.GetResponseStream())
    let txt    = stream.ReadToEnd()
    let parts  = txt.Split('&')
    (parts.[0].Split('=').[1],
     parts.[1].Split('=').[1])


/// Compute the 'Authorization' header for the given request data
let authHeaderAfterAuthenticated url httpMethod token tokenSecret queryParameters =
    let signingKey             = compositeSigningKey secret.consumerSecret tokenSecret
    let queryParams            = oauthParameters secret.consumerKey token
    let signingQueryParameters = queryParams @ queryParameters
    let signingString          = baseString httpMethod url signingQueryParameters
    let oauth_signature        = hmacsha1 signingKey signingString
    ("oauth_signature", oauth_signature) :: queryParams
    |> createAuthorizeHeader 


/// Add an Authorization header to an existing WebRequest
let addAuthHeaderForUser (webRequest : WebRequest) token tokenSecret queryParams =
    let url        = webRequest.RequestUri.ToString()
    let httpMethod = webRequest.Method
    let header     = authHeaderAfterAuthenticated url httpMethod token tokenSecret queryParams
    webRequest.Headers.Add(HttpRequestHeader.Authorization, header)


let captureOAuth() =
    // Compute URL to send user to to allow our app to connect with their credentials,
    // then open the browser to have them accept
    let oauth_token, oauth_token_secret, _ = requestOAuthToken()
    let url = sprintf "%s?oauth_token=%s" authorizeURI oauth_token
    System.Diagnostics.Process.Start("iexplore.exe", url) |> ignore
    (oauth_token, oauth_token_secret)

    // *******NOTE********:
    // Get the 7 digit number from the web page, pass it to the function below to get oauth_token
    // Sample result if things go okay:
    // val oauth_token_secret' : string = "9e571e13-d054-44e6-956a-415ab3ee6d23"
    // val oauth_token' : string = "044da520-0edc-4083-a061-74e115712b61"
    // let oauth_token, oauth_token_secret = accessToken oauth_token'' oauth_token_secret'' ("3030558")



let getTweet ((oauth_token', oauth_token_secret'), pin) =
    let oauth_token, 
        oauth_token_secret = accessToken oauth_token' oauth_token_secret' pin
    let req                = WebRequest.Create(homeTimelineURI)
    addAuthHeaderForUser req oauth_token oauth_token_secret []
    use resp = req.GetResponse()
    use strm = new StreamReader(resp.GetResponseStream())
    strm.ReadToEnd()

// let parms = captureOAuth();;
// getTweet (parms, "0824995");;


let postTweet (tweet: Post) =
    let tweet_ = 
        if tweet.status = "" then 
            sprintf "F# scripted tweet +++ %d #10m" (System.Random(10).Next()) |> urlEncode
        else
            tweet.status |> urlEncode
    let tweetData           = System.Text.Encoding.ASCII.GetBytes("status="+tweet_)
    let queryParameters     = oauthParameters secret.consumerKey secret.accessToken
    let signingString       = baseString "POST" statusURI ([("status",tweet_)] @ queryParameters)
    let signingKey          = compositeSigningKey secret.consumerSecret secret.accessTokenSecret
    let oauth_signature     = hmacsha1 signingKey signingString
    let AuthorizationHeader = ("oauth_signature",oauth_signature) :: queryParameters |> createAuthorizeHeader
    System.Net.ServicePointManager.Expect100Continue <- false
    let req = WebRequest.Create(statusURI)
    req.Headers.Add("Authorization",AuthorizationHeader)
    req.Method        <- "POST"
    req.ContentType   <- "application/x-www-form-urlencoded"
    req.ContentLength <- (int64) tweetData.Length
    use wstream = req.GetRequestStream() 
    wstream.Write(tweetData,0, (tweetData.Length))
    wstream.Flush()
    wstream.Close()
    req.Timeout <- 3 * 60 * 1000
    use resp = req.GetResponse()
    use strm = new StreamReader(resp.GetResponseStream())
    strm.ReadToEnd()


let verifyCredentials () =
    let queryParameters     = oauthParameters secret.consumerKey secret.accessToken
    let signingString       = baseString "GET" verifyCredentialsURI queryParameters
    let signingKey          = compositeSigningKey secret.consumerSecret secret.accessTokenSecret
    let oauth_signature     = hmacsha1 signingKey signingString
    let AuthorizationHeader = ("oauth_signature",oauth_signature) :: queryParameters |> createAuthorizeHeader
    System.Net.ServicePointManager.Expect100Continue <- false
    let req = WebRequest.Create(verifyCredentialsURI)
    //req.AddOAuthHeader(s.accessToken, s.accessTokenSecret, [])
    req.Headers.Add("Authorization",AuthorizationHeader)
    req.Method      <- "GET"
    req.ContentType <- "application/x-www-form-urlencoded"
    use resp = req.GetResponse()
    use strm = new StreamReader(resp.GetResponseStream())
    strm.ReadToEnd()


// searchTweets [("screen_name","@fbmnds")];;
let searchTweets searchParams = 
    try
        let currSearchURI       = searchParams |> Seq.map (fun (k,v) -> k+"="+v) |> String.concat "&" |> sprintf "%s?%s" searchURI
        let queryParameters     = oauthParameters secret.consumerKey secret.accessToken
        let signingString       = baseString "GET" searchURI (queryParameters @ searchParams)
        let signingKey          = compositeSigningKey secret.consumerSecret secret.accessTokenSecret
        let oauth_signature     = hmacsha1 signingKey signingString
        let AuthorizationHeader = ("oauth_signature",oauth_signature) :: queryParameters |> createAuthorizeHeader
        System.Net.ServicePointManager.Expect100Continue <- false
        let req = WebRequest.Create(currSearchURI)
        req.Headers.Add("Authorization",AuthorizationHeader)
        req.Method      <- "GET"
        req.ContentType <- "application/x-www-form-urlencoded"
        use resp = req.GetResponse()
        use strm = new StreamReader(resp.GetResponseStream())
        strm.ReadToEnd()
        |> sprintf """{ "tweets" : %s }""" 
    with _ -> """{ "tweets" : [] }"""



let private getMinMaxId max (searchResult: string) : string option =
    let getMinId_ (sR: string) = 
        try 
            let r = (sR |> JObject.Parse)
            seq { for i in r.Item("tweets") do yield decimal (i.Item("id")) }
            |> if max then Seq.max else Seq.min
            |> string
            |> Some
        with 
        | _ -> None 
    match searchResult with 
    | """{ "tweets" : [] }""" -> None
    | _ -> getMinId_ searchResult

let getMaxId = getMinMaxId true
let getMinId = getMinMaxId false


let insertRecentLeaderFeed limit screen_name =
    try
        let tweets = 
            searchTweets [("screen_name","@"+screen_name)] 
            |> JObject.Parse
            |> fun r -> seq { for i in r.Item("tweets") do yield i }

        if tweets = Seq.empty<JToken> then
            sprintf """{ "msg": "empty Twitter feed for %s" }""" screen_name
        else
            tweets
            |> Seq.take (min limit (Seq.length tweets))
            |> Seq.map (fun i -> 
                let id = i.Item("id").ToString()
                let screen_name' = i.Item("user").Item("screen_name").ToString()
                let status = sprintf "%s\n\nhttps://twitter.com/%s/status/%s" (i.Item("text").ToString()) screen_name id
                let created_at = i.Item("created_at").ToString()
                DataAccess.TwitterAccess.addLeaderTweet screen_name id status created_at)
            |> Seq.fold (fun s t -> if s = "" then t else sprintf "%s,\n%s" s t) ""
            |> sprintf """{ "msg": [%s] }"""
    with _ as e -> sprintf """{ "error_msg": "%s" }""" e.Message       

    
let insertAllRecentLeaderFeeds () =
    try 
        DataAccess.TwitterAccess.selectActiveLeaders ()
        |> List.map (fun l -> 
            Utils.wait 30. 0.3
            printf "current leader %s for Twitter feed insertion" l.UserScreenName
            l.UserScreenName 
            |> insertRecentLeaderFeed Globals.TwitterFeedLimit)
        |> List.fold (fun s t -> if s = "" then t else sprintf "%s,\n%s" s t) ""
            |> sprintf """{ "msg": [%s] }"""
    with _ as e -> sprintf """{ "error_msg": "%s" }""" e.Message


(*
let getAllTweets (q: string) = 
    let nextTweets q (min_id: string option) = 
        match min_id with 
        | Some min_id -> ([("screen_name",q); ("max_id",min_id)] |> searchTweets)
        | _ -> """{ "tweets" : [] }"""
    let rec getRestTweets q minId acc =
        Utils.wait 3. 0.
        let n = nextTweets q minId
        printfn "minId = %A n = %A" minId n
        match n, acc with 
        | """{ "tweets" : [] }""", _ -> acc
        | _, head :: tail when n.Value = head -> acc
        | _, _ -> getRestTweets q (getMinId n) (n.Value :: acc)
    let f = searchTweets [("screen_name",q)]
    getRestTweets q (getMinId f) [f.Value]
*)

let twurlMedia actuser_name post_id =
    try
        let result = 
            sprintf "%s/img/%s-%s.png" Globals.WebRoot actuser_name post_id
            |> sprintf "twurl -H upload.twitter.com -X POST \"/1.1/media/upload.json\" --file \"%s\" --file-field \"media\""
            |> Utils.execute
        printfn "%s" result
        result
        |> JObject.Parse
        |> fun d -> 
            let p = PostRecord ()
            p.ActuserName <- actuser_name 
            p.PostId      <- post_id
            p.MediaId     <- (d.Item("std").ToString() |> Utils.urlDecode |> JObject.Parse).Item("media_id").ToString()
            p |> DataAccess.GabaiAccess.updateMedia       
            (DateTime.UtcNow.ToString("u"))
            |> sprintf """{ "actuser_name": "%s", "post_id": %s, "media_id": "%s", "uploaded_at": "%s" }""" p.ActuserName p.PostId p.MediaId
    with _ as e -> sprintf """{ "error_msg": "%s" }""" e.Message 


let uploadMedia () =
    try
        let filePath = sprintf "%s/img/" Globals.WebRoot
        "thumbnail_created_at is not null AND media_id is null" 
        |> DataAccess.GabaiAccess.selectFromGab
        |> List.map (fun p -> twurlMedia p.ActuserName p.PostId)
        |> List.fold (fun s t -> if s = "" then t else sprintf "%s,\n%s" s t) ""
        |> sprintf """{ "upload" : [%s] }"""
    with _ as e -> sprintf """{ "error_msg": "%s" }""" e.Message
    

//let x () =
//    try
//        let feed = 
//            "samples/tweet-media.json" 
//            |> File.ReadAllText
//            |> JObject.Parse
//        feed.Item("upload").Children()
//        |> Seq.iter (fun d ->   
//            let p = PostRecord ()
//            p.ActuserName <- d.Item("actuser_name").ToString() 
//            p.PostId      <- d.Item("post_id").ToString()
//            p.MediaId     <- d.Item("media_id").ToString()
//            DataAccess.GabaiAccess.updateMedia p) 
//    with _ as e -> printfn """{ "error_msg": "%s" }""" e.Message


let postOfflineTweets () =
    try
        let feed = 
            "samples/tweet-upload-2.json" 
            |> File.ReadAllText
            |> JObject.Parse
        feed.Item("upload")
        |> Seq.map (fun d ->   
            let status    = sprintf "status=%s" (d.Item("status").ToString()) |> urlEncode
            let media_ids = sprintf "media_ids=%s" (d.Item("media_ids").ToString())
            sprintf """twurl /1.1/statuses/update.json -d "%s" -d "%s" """ status media_ids
            |> Utils.execute) 
        |> Seq.fold (fun s t -> if s = "" then t else sprintf "%s,\n%s" s t) ""
        |> sprintf """{  "posted_at": "%s", "uploaded": [\n%s\n] }""" (DateTime.UtcNow.ToString("u"))
    with _ as e -> sprintf """{ "error_msg": "%s" }""" e.Message


let postTweetDb () =
    try
        "tweeted_at is null AND media_id is not null"
        |> selectFromGab
        |> Array.ofSeq
        |> fun x -> Utils.shuffle x; x
        |> Array.map (fun p ->
            try
                let result =
                    let data = sprintf "status=RT gab.ai %s&media_ids=%s" p.ActuserName p.MediaId
                    let args = sprintf """/1.1/statuses/update.json -d "%s" """ data 
                    printfn "%s" args
                    args |> Utils.twurl
                let e1 = (result |> JObject.Parse).Item("err").ToString() = ""
                let e2 = (result |> JObject.Parse).Item("err").ToString().Contains("errors") |> not
                if e1 && e2 then updatePost p updateCmdTweet
                printfn "%s" result
                Utils.wait 60. 0.2
                result
            with _ as e -> sprintf """{ "error_msg": "%s" }""" e.Message)
        |> Array.fold (fun s t -> if s = "" then t else sprintf "%s,\n%s" s t) ""
        |> sprintf """{  "tweeted_at": "%s", "tweeted": [\n%s\n] }""" (DateTime.UtcNow.ToString("u"))
    with _ as e -> sprintf """{ "error_msg": "%s" }""" e.Message

