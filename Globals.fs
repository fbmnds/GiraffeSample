
// ---------------------------------
// Global settings
// ---------------------------------


module Globals 


// ---------------------------------
// Path settings
// ---------------------------------

let ContentRoot = System.IO.Directory.GetCurrentDirectory()
let WebRoot     = System.IO.Path.Combine(ContentRoot, "WebRoot")
let Secrets     = System.Environment.GetEnvironmentVariable("SECRETS")
let Home        = System.Environment.GetEnvironmentVariable("HOME")


// ---------------------------------
// App settings
// ---------------------------------

let UserAgent   = "phpGab/1.0"