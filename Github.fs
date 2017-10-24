module Github

open System
open System.Net.Http
open System.Net.Http.Headers
open System.Runtime.Serialization
open System.Threading.Tasks


// ---------------------------------
// Github API
// ---------------------------------

let apiUrl = "https://api.github.com/orgs/dotnet/repos"


[<DataContract>]
[<CLIMutable>]
type Repository =  
    { [<DataMember(Name="name")>]        Name          : string
      [<DataMember(Name="description")>] Description   : string
      [<DataMember(Name="html_url")>]    GitHubHomeUrl : Uri
      [<DataMember(Name="homepage")>]    Homepage      : Uri
      [<DataMember(Name="watchers")>]    Watchers      : int
      [<DataMember(Name="pushed_at")>]   JsonDate      : string
    }    
    //[<IgnoreDataMember>]
    //static member LastPush with get() = DateTime.ParseExact(__.JsonDate, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)

type RepositoryArray = Repository array

    
let processRepositories () =
    use client = new HttpClient()
    client.DefaultRequestHeaders.Accept.Clear()
    client.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"))
    client.DefaultRequestHeaders.Add("User-Agent", Globals.UserAgent)
    let stringTask = client.GetStringAsync(apiUrl).Result
    stringTask, Utils.DeserializeJson<RepositoryArray>(stringTask)
        
    
let _,repositories = processRepositories()
    
let printRepos =
    for repo in repositories do
        Console.WriteLine(repo.Name)
        Console.WriteLine(repo.Description)
        Console.WriteLine(repo.GitHubHomeUrl)
        Console.WriteLine(repo.Homepage)
        Console.WriteLine(repo.Watchers)
        Console.WriteLine(repo.JsonDate)
        Console.WriteLine()
        Console.ReadLine() |> ignore

let offlineRepositories () = "samples/repos.json" |> IO.File.ReadAllText


