module GiraffeSample.App

open System
open System.IO
open System.Net
open System.Threading.Tasks
open System.Collections.Generic
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
open Giraffe.Razor.HttpHandlers
open Giraffe.Razor.Middleware
open GiraffeSample.Models
open Giraffe.HttpContextExtensions

open LunchTypes
open DataAccess
open Twitter

open Newtonsoft.Json
open Newtonsoft.Json.Linq


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

let handleTwitter (next: HttpFunc) (ctx: HttpContext) =
    task {
        let twitter = Twitter.searchTweets [("screen_name","@fbmnds")] |> JObject.Parse
        let tweets = (twitter.Item("tweets")).First.ToString()
        return! text tweets next ctx
    }

// ---------------------------------
// Web app
// ---------------------------------

let webApp =
    choose [
        GET >=> route "/tweets" >=> handleTwitter
        GET >=> route "/lunch" >=> handleLunchFilter
        POST >=> route "/lunch/add" >=> handleAddLunch
        POST >=> route "/lunch/del" >=> handleDelLunch
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
    let options = (new RewriteOptions()).AddRedirectToHttps()
    app.UseGiraffeErrorHandler errorHandler
    app.UseStaticFiles() |> ignore
    app.UseRewriter(options) |> ignore
    app.UseGiraffe webApp
    

let configureServices (services : IServiceCollection) =
    let sp  = services.BuildServiceProvider()
    let env = sp.GetService<IHostingEnvironment>()
    let viewsFolderPath = Path.Combine(env.ContentRootPath, "Views")
    let options = (new RewriteOptions()).AddRedirectToHttps()
    services.Configure<MvcOptions>(fun options -> options.Filters.Add(new RequireHttpsAttribute())) |> ignore
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
            //options.Listen(IPAddress.Any, 57877) |> ignore
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
*)