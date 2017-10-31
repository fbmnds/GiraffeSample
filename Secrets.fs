module Secrets

open System
open System.IO

open NPoco
open Microsoft.Data.Sqlite
open Newtonsoft.Json.Linq


type Secret = 
    { 
        consumerKey       : string
        consumerSecret    : string
        accessToken       : string
        accessTokenSecret : string 
        gabUsername       : string
        gabPassword       : string  
        giraffeSamplePfx  : string
    }

let mutable secret : Secret =
    let s = 
        "secret.json"
        |> if Environment.OSVersion.ToString().Contains("Windows") then
            sprintf "%s\\%s" Globals.Vault
           else
            sprintf "%s/%s" Globals.Vault
        |> File.ReadAllText
        |> JObject.Parse
    { 
        consumerKey       = s.Item("consumerKey").ToString()
        consumerSecret    = s.Item("consumerSecret").ToString()
        accessToken       = s.Item("accessToken").ToString()
        accessTokenSecret = s.Item("accessTokenSecret").ToString()
        gabUsername       = s.Item("gabUsername").ToString()
        gabPassword       = s.Item("gabPassword").ToString()
        giraffeSamplePfx  = s.Item("giraffeSamplePfx").ToString()
    }


type JwtToken () =
    member val secretHash   = Utils.hash256(secret.gabUsername + secret.gabPassword) with get, set
    member val jwtHeader    = String(Array.create  36 ' ') with get, set
    member val jwtPayload   = String(Array.create 158 ' ') with get, set
    member val jwtSignature = String(Array.create  43 ' ') with get, set
    member val exp          = "1970-01-01 00:00:00+00:00" with get, set



let mutable gabJwtToken = 
    let jwt = JwtToken ()
    let query = sprintf "select * from gabjwt where secretHash='%s'" jwt.secretHash
    try
        use conn = new SqliteConnection(Globals.connString)
        conn.Open()
        use db = new Database(conn)
        db.Fetch<JwtToken>(query) |> Seq.head
    with _ as e -> 
        printfn "connection '%s', select of JWT token for '%s' failed:\n %s" 
            Globals.connString secret.gabUsername e.Message
        try
            let insertCmd = @"
insert into gabjwt (secretHash, jwtHeader, jwtPayload, jwtSignature, exp)
values ($SecretHash, $JwtHeader, $JwtPayload, $JwtSignature, $Exp)"
            use conn = new SqliteConnection(Globals.connString)
            conn.Open()        
            use txn: SqliteTransaction = conn.BeginTransaction()
            let cmd = conn.CreateCommand()
            cmd.Transaction <- txn
            cmd.CommandText <- insertCmd
            cmd.Parameters.AddWithValue("$SecretHash",   jwt.secretHash)   |> ignore
            cmd.Parameters.AddWithValue("$JwtHeader",    jwt.jwtHeader)    |> ignore
            cmd.Parameters.AddWithValue("$JwtPayload",   jwt.jwtPayload)   |> ignore
            cmd.Parameters.AddWithValue("$JwtSignature", jwt.jwtSignature) |> ignore
            cmd.Parameters.AddWithValue("$Exp",          jwt.exp)          |> ignore
            cmd.ExecuteNonQuery() |> ignore
            txn.Commit()
        with _ as e ->
            printfn "connection '%s', insert of JWT token for '%s' failed:\n %s" 
                Globals.connString secret.gabUsername e.Message        
        jwt