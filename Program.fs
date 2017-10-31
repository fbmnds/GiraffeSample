module GiraffeSample.App

open System
open System.IO
open System.Net
open System.Security.Cryptography.X509Certificates

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Rewrite
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection

open Giraffe
open Giraffe.HttpHandlers
open Giraffe.Middleware
open Giraffe.Razor.Middleware
open Giraffe.HttpContextExtensions


open Secrets
open DataAccess
open Globals
open DataAccess.Types






// ---------------------------------
// Twitter
// ---------------------------------

let handleTwitterFeed name (next: HttpFunc) (ctx: HttpContext) =
    task {
        let tweets = Twitter.searchTweets [("screen_name", (sprintf "@%s" name))]
        return! text tweets next ctx
    }

let handleTwitterLeaderAdd name (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (TwitterAccess.addLeader Twitter.searchTweets name) next ctx
    }


let handleTwitterLeaderFeedAdd name (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Twitter.insertRecentLeaderFeed TwitterFeedLimit name) next ctx
    }


let handleTwitterAllLeaderFeedsAdd (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Twitter.insertAllRecentLeaderFeeds ()) next ctx
    }

let handleTwitterImgUpload (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Twitter.uploadMedia ()) next ctx
    }


let handleTwitterPost (next: HttpFunc) (ctx: HttpContext) =
    task {
        let! post =  ctx.BindJson<Twitter.Post>()
        return! text (Twitter.postTweet post) next ctx
    }


let handleTwitterOfflinePost (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Twitter.postOfflineTweets ()) next ctx
    }



let handleTwitterPostDb (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Twitter.postTweetDb ()) next ctx
    }


// ---------------------------------
// Gab.ai
// ---------------------------------

let handleGabPost body (next: HttpFunc) (ctx: HttpContext) =
    task {
        let post = GabPost ()
        post.body <- body
        return! text (Gabai.Api.postGab Secrets.secret post) next ctx
    } 

///https://twitter.com/OssiMarzahn76/status/923533897908015104
let handleFeedGabPost body (next: HttpFunc) (ctx: HttpContext) =
    task {
        let post = FeedRecord ()
        post.Status <- "https://twitter.com/OssiMarzahn76/status/923533897908015104"
        post.TweetId <- "923533897908015104"
        post.UserScreenName <- "OssiMarzahn76"
        
        return! text (Gabai.Api.postTweetOnGab Secrets.secret post) next ctx
    } 


let handleGabThumbnail name feed (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Gabai.Thumbnail.generateThumbnail name feed) next ctx
    }


let handleGabThumbnailDb (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Gabai.Thumbnail.generateThumbnailFromDb ()) next ctx
    }


let handleGabLogin (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Gabai.Api.getToken Secrets.secret) next ctx
    }


let handleGabFeed name (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Gabai.Api.getFeed Secrets.secret name) next ctx
    }


let handleInsertGabFeed name (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Gabai.Api.insertFeed Secrets.secret name) next ctx
    }


let handleGabOfflineTweetFeed name (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Gabai.Api.offlineTweetFeed name) next ctx
    }


let handleGabSelectFor clause (next: HttpFunc) (ctx: HttpContext) =
    task {
        let result =
            if clause = "thumbnails" then 
                "thumbnail_created_at is null"
                |> GabaiAccess.selectFromGab
                |> List.fold (fun s p -> sprintf "%s\n%s, %s" s p.ActuserName p.PostId) ""
            else
                "tweeted_at is null"
                |> GabaiAccess.selectFromGab
                |> List.fold (fun s p -> sprintf "%s\n%s, %s" s p.ActuserName p.PostId) ""
        return! text result next ctx
    }


let handleGabTwitterLeaderFeedsPost (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Gabai.Api.postTwitterLeadersOnGab Secrets.secret) next ctx
    }


// ---------------------------------
// Github
// ---------------------------------

let handleGithub (next: HttpFunc) (ctx: HttpContext) =
    task {
        let repos,_ = Github.processRepositories()
        return! text repos next ctx
    }

let handleGithubOffline (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Github.offlineRepositories()) next ctx
    }


// ---------------------------------
// Web app
// ---------------------------------

/// https://kali:57878/gab/db/insert/feed/<gabname>
/// https://kali:57878/gab/db/thumbnail
/// https://kali:57878/tweets/img/upload
/// https://kali:57878/tweets/db/post

/// https://kali:57878/tweets/leader/feeds/add
/// https://kali:57878/gab/twitter/leader/feeds/post

let webApp =
    choose [       
        GET  >=> route  "/tweets/db/post"                >=> handleTwitterPostDb        
        GET  >=> routef "/tweets/feed/%s"                (fun name -> (handleTwitterFeed name))
        GET  >=> route  "/tweets/img/upload"             >=> handleTwitterImgUpload
        GET  >=> routef "/tweets/leader/add/%s"          (fun name -> (handleTwitterLeaderAdd name))
        GET  >=> routef "/tweets/leader/feed/add/%s"     (fun name -> (handleTwitterLeaderFeedAdd name))
        GET  >=> route  "/tweets/leader/feeds/add"       >=> handleTwitterAllLeaderFeedsAdd
        GET  >=> route  "/tweets/offline/post"           >=> handleTwitterOfflinePost
        POST >=> route  "/tweets/post"                   >=> handleTwitterPost
        
        GET  >=> route  "/gab/login"                     >=> handleGabLogin
        GET  >=> routef "/gab/post/%s"                   (fun body -> (handleGabPost body))
        GET  >=> routef "/gab/db/insert/feed/%s"         (fun name -> (handleInsertGabFeed name))
        GET  >=> routef "/gab/db/select/clause/%s"       (fun clause -> (handleGabSelectFor clause))
        GET  >=> route  "/gab/db/thumbnail"              >=> handleGabThumbnailDb
        GET  >=> routef "/gab/feed/%s"                   (fun name -> (handleGabFeed name))
        GET  >=> routef "/gab/file/thumbnail/%s/%s"      (fun (name,post) -> (handleGabThumbnail name post))
        
        GET  >=> routef "/gab/offline/tweet/feed/%s"     (fun name -> (handleGabOfflineTweetFeed name))
        GET  >=> route  "/gab/twitter/leader/feeds/post" >=> handleGabTwitterLeaderFeedsPost   
        //GET  >=> routef "/tweets/feed/post/%s"           (fun name -> (handleGabTwitterFeedsPost name))
        GET  >=> route  "/gab/test"                       >=> handleFeedGabPost "test"
        GET  >=> route  "/github/repos"                  >=> handleGithub
        GET  >=> route  "/github/offline/repos"          >=> handleGithubOffline

        setStatusCode 404 >=> text "Not Found" 
    ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureApp (app : IApplicationBuilder) =
    let options = (RewriteOptions()).AddRedirectToHttps()
    app.UseGiraffeErrorHandler errorHandler
    app.UseStaticFiles() |> ignore
    app.UseRewriter(options) |> ignore
    app.UseGiraffe webApp
    

let configureServices (services : IServiceCollection) =
    let sp  = services.BuildServiceProvider()
    let env = sp.GetService<IHostingEnvironment>()
    let viewsFolderPath = Path.Combine(env.ContentRootPath, "Views")
    let options = (RewriteOptions()).AddRedirectToHttps()
    services.Configure<MvcOptions>(fun options -> options.Filters.Add(RequireHttpsAttribute())) |> ignore
    services.AddRazorEngine viewsFolderPath |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

[<EntryPoint>]
[<RequireHttps>]
let main argv =
    let pfxFile     = Environment.GetEnvironmentVariable("SECRETS") + @"/GiraffeSample.pfx"
    let certificate = new X509Certificate2(pfxFile, secret.giraffeSamplePfx)
    WebHostBuilder()
        .UseKestrel(fun options -> 
            options.Listen(IPAddress.Any, 57878, (fun listenOptions -> listenOptions.UseHttps(pfxFile, secret.giraffeSamplePfx) |> ignore)))
        .UseIISIntegration()
        .UseWebRoot(WebRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0

(*
curl -k -i -X POST -H 'Content-Type: application/json' \
 -d '{"ID":4,"Name":"Malay Satay Hut","Latitude":47.631949,"Longitude":-122.136367,"Cuisine":"Malaysian","VegetarianOptions":true,"VeganOptions":true}' \
 https://localhost:57878/lunch/add

curl -k -i -X POST -H 'Content-Type: application/json' \
 -d '{"ID":4}' \
 https://localhost:57878/lunch/del

curl -k -i -X POST -H 'Content-Type: application/json' \
 -d '{"status":""}' \
 https://localhost:57878/tweets/post

curl -k -i -X POST -H 'Content-Type: application/json' \
 -d '{"status":"via gab.ai https://gab.ai/Bergschreck/posts/13109522"}' \
 https://localhost:57878/tweets/post

curl -k -i -X POST -H 'Content-Type: application/json' \
 -d '{"status":"via unzensuriert.at  https://t.co/pKACAuWp8M"}' \
 https://localhost:57878/tweets/post


*)
