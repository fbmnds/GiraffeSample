module Twitter

open System
open System.IO
open System.Net
open System.Security.Cryptography
open System.Text

open Newtonsoft.Json.Linq


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

type Secret = 
    { 
        consumerKey       : string
        consumerSecret    : string
        accessToken       : string
        accessTokenSecret : string 
    }

let secret : Secret =
    let s = 
        let home = Environment.GetEnvironmentVariable("HOME")
        if home.Contains(@":\") then home + @"\.ssh\secret.json" else home + @"/.ssh/secret.json"
        |> File.ReadAllText
        |> JObject.Parse
    { 
        consumerKey       = s.Item("consumerKey").ToString()
        consumerSecret    = s.Item("consumerSecret").ToString()
        accessToken       = s.Item("accessToken").ToString()
        accessTokenSecret = s.Item("accessTokenSecret").ToString()
    }

let requestTokenURI      = "https://api.twitter.com/oauth/request_token"
let accessTokenURI       = "https://api.twitter.com/oauth/access_token"
let authorizeURI         = "https://api.twitter.com/oauth/authorize"
let verifyCredentialsURI = "https://api.twitter.com/1.1/account/verify_credentials.json"
let searchURI            = "https://api.twitter.com/1.1/statuses/user_timeline.json" 
let homeTimelineURI      = "https://api.twitter.com/1.1/statuses/home_timeline.json"
let statusURI            = "https://api.twitter.com/1.1/statuses/update.json"

// Utilities

let unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
let urlEncode str =
    String.init (String.length str) (fun i ->
        let symbol = str.[i]
        if unreservedChars.IndexOf(symbol) = -1 then
            "%" + String.Format("{0:X2}", int symbol)
        else
            string symbol)

// Core Algorithms
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
  //req.AddOAuthHeader(s.accessToken, s.accessTokenSecret, [])
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
      |> sprintf "{\"tweets\":%s}" 
    with
    | _ -> "{\"tweets\":[]}"

  // searchTweets [("screen_name","@fbmnds")];;

(*

let getMinId (searchResult: string option) : string option =
    let getMinId_ (sR: string option) = 
        try 
            let r = (JsonValue.Parse sR.Value)
            let s = seq { for i in r.GetProperty("statuses") do yield (i?id) }
            s
            |> Seq.min
            |> string
            |> Some
        with 
        | _ -> None 
    match searchResult with 
    | Some searchresult -> getMinId_ searchResult
    | _ -> None


let getAllTweets (q: string) = 
    let nextTweets q (min_id: string option) : string option = 
        match min_id with 
        | Some min_id -> ([("screen_name",q); ("max_id",min_id)] |> searchTweets)
        | _ -> None
    let rec getRestTweets q minId acc =
        System.Threading.Thread.Sleep(3000)
        let n = nextTweets q minId
        printfn "minId = %A n = %A" minId n
        match n, acc with 
        | None, _ -> acc
        | _, head :: tail when n.Value = head -> acc
        | _, _ -> getRestTweets q (getMinId n) (n.Value :: acc)
    let f = searchTweets [("screen_name",q)]
    getRestTweets q (getMinId f) [f.Value]
*)
// getAllTweets "@fbmnds";;