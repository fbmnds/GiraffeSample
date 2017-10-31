
// ---------------------------------
// Global settings
// ---------------------------------


module Globals 

open System.IO


// ---------------------------------
// Path settings
// ---------------------------------

let ContentRoot = System.IO.Directory.GetCurrentDirectory()
let WebRoot     = System.IO.Path.Combine(ContentRoot, "WebRoot")
let Vault       = System.Environment.GetEnvironmentVariable("SECRETS")
let Home        = System.Environment.GetEnvironmentVariable("HOME")


// ---------------------------------
// Database settings
// ---------------------------------

let connString = "Filename=" + System.IO.Path.Combine(ContentRoot, "Sample.db")
let TwitterFeedLimit = 20
let TwitterFeedChunkSize = 3


// ---------------------------------
// App settings
// ---------------------------------

let UserAgent   = "phpGab/1.0"