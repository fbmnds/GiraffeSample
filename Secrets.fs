module Secrets

open System
open System.IO

open Newtonsoft.Json.Linq

type Secret = 
    { 
        consumerKey       : string
        consumerSecret    : string
        accessToken       : string
        accessTokenSecret : string 
        gabUsername       : string
        gabPassword       : string
        gabJwtHeader      : string
        gabJwtPayload     : string
        gabJwtSignature   : string  
        giraffeSamplePfx  : string
    }

let secret : Secret =
    let s = 
        "secret.json"
        |> if Environment.OSVersion.ToString().Contains("Windows") then
            sprintf "%s\\%s" Globals.Secrets
           else
            sprintf "%s/%s" Globals.Secrets
        |> File.ReadAllText
        |> JObject.Parse
    { 
        consumerKey       = s.Item("consumerKey").ToString()
        consumerSecret    = s.Item("consumerSecret").ToString()
        accessToken       = s.Item("accessToken").ToString()
        accessTokenSecret = s.Item("accessTokenSecret").ToString()
        gabUsername       = s.Item("gabUsername").ToString()
        gabPassword       = s.Item("gabPassword").ToString()
        gabJwtHeader      = s.Item("gabJwtHeader").ToString()
        gabJwtPayload     = s.Item("gabJwtPayload").ToString()
        gabJwtSignature   = s.Item("gabJwtSignature").ToString()
        giraffeSamplePfx  = s.Item("giraffeSamplePfx").ToString()
    }
