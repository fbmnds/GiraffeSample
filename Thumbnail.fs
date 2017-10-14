module Thumbnail


let execute name post =
    let command = sprintf "QT_QPA_PLATFORM=offscreen phantomjs thumbnail.js https://gab.ai/%s/posts/%s WebRoot/%s.png" name post post
    use proc = new System.Diagnostics.Process()

    proc.StartInfo.FileName <- "/bin/bash"
    proc.StartInfo.Arguments <- "-c \" " + command + " \""
    proc.StartInfo.UseShellExecute <- false
    proc.StartInfo.RedirectStandardOutput <- true
    proc.StartInfo.RedirectStandardError <- true
    proc.Start() |> ignore

    let std = proc.StandardOutput.ReadToEnd()
    let err = proc.StandardError.ReadToEnd()

    proc.WaitForExit()

    std, err