
// ---------------------------------
// Global settings
// ---------------------------------


module Globals 


let TwitterFeedLimit = 20
let TwitterFeedChunkSize = 3

// ---------------------------------
// Path settings
// ---------------------------------

let ContentRoot = System.IO.Directory.GetCurrentDirectory()
let WebRoot     = System.IO.Path.Combine(ContentRoot, "WebRoot")
let Vault       = System.Environment.GetEnvironmentVariable("SECRETS")
let Home        = System.Environment.GetEnvironmentVariable("HOME")


// ---------------------------------
// App settings
// ---------------------------------

let UserAgent   = "phpGab/1.0"