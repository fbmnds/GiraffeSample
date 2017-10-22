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

open Newtonsoft.Json.Linq

open LunchTypes
open DataAccess


// ---------------------------------
// Data access example 'lunch'
// ---------------------------------

let handleLunchFilter (next: HttpFunc) (ctx: HttpContext) =
    let filter = ctx.BindQueryString<LunchFilter>()
    let lunchSpots = LunchAccess.getLunches filter
    json lunchSpots next ctx


let handleAddLunch (next: HttpFunc) (ctx: HttpContext) =
    task {
        let! lunch = ctx.BindJson<LunchSpot>()
        LunchAccess.addLunch lunch
        return! text (sprintf "Added %s to the lunch spots." lunch.Name) next ctx
    }

let handleDelLunch (next: HttpFunc) (ctx: HttpContext) =
    task {
        let! lunch = ctx.BindJson<LunchID>()
        LunchAccess.delLunch lunch
        return! text (sprintf "Deleted %A from lunch spots." lunch.ID) next ctx
    }


// ---------------------------------
// Twitter
// ---------------------------------

let handleTwitterFeed name (next: HttpFunc) (ctx: HttpContext) =
    task {
        let twitter = Twitter.searchTweets [("screen_name", (sprintf "@%s" name))] |> JObject.Parse
        let tweets = (twitter.Item("tweets")).First.ToString()
        return! text tweets next ctx
    }


let handleTwitterPost (next: HttpFunc) (ctx: HttpContext) =
    task {
        let! post =  ctx.BindJson<Twitter.Post>()
        return! text (Twitter.postTweet post) next ctx
    }


// ---------------------------------
// Gab.ai
// ---------------------------------

let handleGabThumbnail name feed (next: HttpFunc) (ctx: HttpContext) =
    task {
        let std,err = Thumbnail.execute name feed
        return! text (sprintf "%s\n%s" std err) next ctx
    }


let handleGabLogin (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Gabai.getToken ()) next ctx
    }


let handleGabFeed name (next: HttpFunc) (ctx: HttpContext) =
    task {
        return! text (Gabai.getFeed name) next ctx
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

let webApp =
    choose [
        POST >=> route  "/tweets/post"    >=> handleTwitterPost
        GET  >=> routef "/tweets/feed/%s" (fun name -> (handleTwitterFeed name))

        GET  >=> route  "/gab/login"           >=> handleGabLogin
        GET  >=> routef "/gab/thumbnail/%s/%s" (fun (name,post) -> (handleGabThumbnail name post))
        GET  >=> routef "/gab/feed/%s"         (fun name -> (handleGabFeed name))
        
        GET  >=> route  "/github/repos"         >=> handleGithub
        GET  >=> route  "/github/offline/repos" >=> handleGithubOffline

        GET  >=> route  "/lunch"     >=> handleLunchFilter
        POST >=> route  "/lunch/add" >=> handleAddLunch
        POST >=> route  "/lunch/del" >=> handleDelLunch

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
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    let pfxFile     = Path.Combine(contentRoot, "GiraffeSample.pfx")
    let certificate = new X509Certificate2(pfxFile, "GiraffeSample")
    WebHostBuilder()
        .UseKestrel(fun options -> 
            options.Listen(IPAddress.Any, 57878, (fun listenOptions -> listenOptions.UseHttps(pfxFile, "GiraffeSample") |> ignore)))
        .UseIISIntegration()
        .UseWebRoot(webRoot)
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
