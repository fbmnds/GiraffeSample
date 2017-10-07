module Github

open System
open System.Collections.Generic
open System.Net.Http
open System.Net.Http.Headers
open System.Runtime.Serialization
open System.Runtime.Serialization.Json
open System.Globalization
open System.Threading.Tasks

[<DataContract(Name="repo")>]
type Repository () =  
    [<DataMember(Name="name")>]
    member val Name = "" with get, set

    [<DataMember(Name="description")>] 
    member val Description = "" with get, set
        
    [<DataMember(Name="html_url")>]
    member val GitHubHomeUrl = Uri("") with get, set
        
    [<DataMember(Name="homepage")>]
    member val Homepage = Uri("") with get, set
        
    [<DataMember(Name="watchers")>]
    member val Watchers = 0 with get, set
        
    [<DataMember(Name="pushed_at")>]
    member val JsonDate = "" with get, set
        
    //[<IgnoreDataMember>]
    //static member LastPush with get() = DateTime.ParseExact(__.JsonDate, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)

type RepositoryArray = Repository array

let toString = System.Text.Encoding.ASCII.GetString
let toBytes (x : string) = System.Text.Encoding.ASCII.GetBytes x

let serializeJson<'a> (x : 'a) = 
    let jsonSerializer = DataContractJsonSerializer(typedefof<'a>)
    use stream = new IO.MemoryStream()
    jsonSerializer.WriteObject(stream, x)
    toString <| stream.ToArray()

let deserializeJson<'a> (json : string) =
    let jsonSerializer = DataContractJsonSerializer(typedefof<'a>)
    use stream = new IO.MemoryStream(toBytes json)
    jsonSerializer.ReadObject(stream) :?> 'a




let apiUrl = "https://api.github.com/orgs/dotnet/repos"
    
let processRepositories () =
    use client = new HttpClient()
    client.DefaultRequestHeaders.Accept.Clear()
    client.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"))
    client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter")
    let stringTask = client.GetStringAsync(apiUrl).Result
    //IO.File.WriteAllText("repos.json", stringTask)
    stringTask, deserializeJson<RepositoryArray>(stringTask)
        
    
let _,repositories = processRepositories()
    
let printRepos =
    for repo in repositories do
        Console.WriteLine(repo.Name)
        Console.WriteLine(repo.Description)
        Console.WriteLine(repo.GitHubHomeUrl)
        Console.WriteLine(repo.Homepage)
        Console.WriteLine(repo.Watchers)
        Console.WriteLine(repo.JsonDate)
    //    Console.WriteLine(repo.LastPush)
        Console.WriteLine()
        Console.ReadLine() |> ignore

let offlineRepositories () = "repos.json" |> IO.File.ReadAllText


