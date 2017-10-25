namespace DataAccess

open System.IO
open NPoco
open Microsoft.Data.Sqlite

open DataAccess.Types


type Bool =
| True = 1
| False = 0

module TwitterAccess =
    let private connString = "Filename=" + Path.Combine(Directory.GetCurrentDirectory(), "Sample.db")
    let private insertLeaderCmd = sprintf @"
insert into twitter_leader (%s) values ($UserScreenName, $Active)" LeaderRecordColumns
    let private insertLeaderFeedCmd = sprintf @"
insert into twitter_leader_feeds (%s) 
values ($UserScreenName, $TweetId, $Status, $TweetedAt, null)" FeedRecordColumns //"user_screen_name,tweet_id,status,tweeted_at,gabbed_at"
    let private selectActiveLeadersCmd = "select user_screen_name from twitter_leader where active is 1"


    let addLeader (searchTweets: (string*string) list -> string) user_screen_name =
        try
            if (searchTweets [("screen_name","@"+user_screen_name)]) = """{ "tweets" : [] }""" then
                sprintf """{  "error_msg": "no tweets found for %s, ignored" } """ user_screen_name
            else
                use conn = new SqliteConnection(connString)
                conn.Open()        
                use txn: SqliteTransaction = conn.BeginTransaction()
                let cmd = conn.CreateCommand()
                cmd.Transaction <- txn
                cmd.CommandText <- insertLeaderCmd
                cmd.Parameters.AddWithValue("$UserScreenName", user_screen_name) |> ignore
                cmd.Parameters.AddWithValue("$Active",         Bool.True)        |> ignore
                cmd.ExecuteNonQuery() |> ignore
                txn.Commit()
                sprintf """{  "msg": "%s added to Twitter leader board" } """ user_screen_name
        with _ as e -> sprintf """{  "error_msg": "connection '%s', insert of user '%s' failed:\n %s" }""" 
                            connString user_screen_name e.Message


    let addLeaderTweet user_screen_name tweet_id status tweeted_at =
        try
            use conn = new SqliteConnection(connString)
            conn.Open()        
            use txn: SqliteTransaction = conn.BeginTransaction()
            let cmd = conn.CreateCommand()
            cmd.Transaction <- txn
            cmd.CommandText <- insertLeaderFeedCmd
            cmd.Parameters.AddWithValue("$UserScreenName", user_screen_name) |> ignore
            cmd.Parameters.AddWithValue("$TweetId",        tweet_id)         |> ignore
            cmd.Parameters.AddWithValue("$Status",         status)           |> ignore
            cmd.Parameters.AddWithValue("$TweetedAt",      tweeted_at)       |> ignore
            cmd.ExecuteNonQuery() |> ignore
            txn.Commit()
            let result = sprintf """{  "msg": "%s,'%s' added to Twitter leader feed" } """ user_screen_name tweet_id
            printfn "%s" result
            result
        with _ as e -> sprintf """{  "error_msg": "connection '%s', insert of user feed '%s','%s' failed:\n %s" }""" 
                            connString user_screen_name tweet_id e.Message



    let addLeaderFeed limit user_screen_name = 0


    // select * from twitter where active is 1;
    let selectActiveLeaders () = 
        try
            use conn = new SqliteConnection(connString)
            conn.Open()
            use db = new Database(conn)
            db.Fetch<LeaderRecord>(selectActiveLeadersCmd) |> List.ofSeq
        with _ as e -> printfn "connection '%s', active Twitter leaders select failed:\n %s" connString e.Message; [] //System.Collections.Generic.List<PostRecord>()   



module GabaiAccess =
    let private connString = "Filename=" + Path.Combine(Directory.GetCurrentDirectory(), "Sample.db")
    let private insertCmd =  @"
insert into gab (actuser_name, post_id, post_body, post_created_at)
values ($ActuserName, $PostId, $PostBody, $PostCreatedAt)"
    let updateCmdThumbnail =  @"
update gab set thumbnail_created_at=$TimeStamp
where actuser_name=$ActuserName and post_id=$PostId"
    let updateCmdMedia =  @"
update gab set media_id=$MediaId
where actuser_name=$ActuserName and post_id=$PostId"
    let updateCmdTweet =  @"
update gab set tweeted_at=$TimeStamp
where actuser_name=$ActuserName and post_id=$PostId"

    let addPost (post: PostRecord) =
        try
            use conn = new SqliteConnection(connString)
            conn.Open()        
            use txn: SqliteTransaction = conn.BeginTransaction()
            let cmd = conn.CreateCommand()
            cmd.Transaction <- txn
            cmd.CommandText <- insertCmd
            cmd.Parameters.AddWithValue("$ActuserName",   post.ActuserName) |> ignore
            cmd.Parameters.AddWithValue("$PostId",        post.PostId) |> ignore
            cmd.Parameters.AddWithValue("$PostBody",      post.PostBody) |> ignore
            cmd.Parameters.AddWithValue("$PostCreatedAt", post.PostCreatedAt) |> ignore
            cmd.ExecuteNonQuery() |> ignore
            txn.Commit()
        with _ as e -> printfn "connection '%s', insert of '%A' failed:\n %s" connString post e.Message
 

    let addPostSeq (posts: PostRecord seq) =
        try
            use conn = new SqliteConnection(connString)
            conn.Open()
            for post in posts do
                try
                    use txn: SqliteTransaction = conn.BeginTransaction()
                    let cmd = conn.CreateCommand()
                    cmd.Transaction <- txn
                    cmd.CommandText <- insertCmd
                    cmd.Parameters.AddWithValue("$ActuserName",   post.ActuserName) |> ignore
                    cmd.Parameters.AddWithValue("$PostId",        post.PostId) |> ignore
                    cmd.Parameters.AddWithValue("$PostBody",      post.PostBody) |> ignore
                    cmd.Parameters.AddWithValue("$PostCreatedAt", post.PostCreatedAt) |> ignore
                    cmd.ExecuteNonQuery() |> ignore
                    txn.Commit()
                with _ as e -> printfn "insert of '%A' failed: %s" post e.Message
        with _ as e -> printfn "connection '%s' failed:\n %s" connString e.Message      


    // select * from gab where tweeted_at is null;
    // select * from gab where thumbnail_created_at is null;
    let selectFromGab clause =
        let query = sprintf "select %s from gab where %s" DataAccess.Types.PostRecordColumns clause
        try
            use conn = new SqliteConnection(connString)
            conn.Open()

            use db = new Database(conn)
            db.Fetch<PostRecord>(query) |> List.ofSeq
        with _ as e -> printfn "connection '%s', select of '%s' failed:\n %s" connString clause e.Message; [] //System.Collections.Generic.List<PostRecord>()   


    let updatePost (post: PostRecord) updateCmd =
        try
            use conn = new SqliteConnection(connString)
            conn.Open()
            use txn: SqliteTransaction = conn.BeginTransaction()
            let cmd = conn.CreateCommand()
            cmd.Transaction <- txn
            cmd.CommandText <- updateCmd
            cmd.Parameters.AddWithValue("$ActuserName",  post.ActuserName) |> ignore
            cmd.Parameters.AddWithValue("$PostId",       post.PostId) |> ignore
            cmd.Parameters.AddWithValue("$TimeStamp",    System.DateTime.UtcNow.ToString("u")) |> ignore
            cmd.ExecuteNonQuery() |> ignore
            txn.Commit()            
        with _ as e -> printfn "connection '%s', update of '%A' failed:\n %s" connString post e.Message


    let updateMedia (post: PostRecord) =
        try
            use conn = new SqliteConnection(connString)
            conn.Open()
            use txn: SqliteTransaction = conn.BeginTransaction()
            let cmd = conn.CreateCommand()
            cmd.Transaction <- txn
            cmd.CommandText <- updateCmdMedia
            cmd.Parameters.AddWithValue("$ActuserName",  post.ActuserName) |> ignore
            cmd.Parameters.AddWithValue("$PostId",       post.PostId) |> ignore
            cmd.Parameters.AddWithValue("$MediaId",      post.MediaId) |> ignore
            cmd.ExecuteNonQuery() |> ignore
            txn.Commit()            
        with _ as e -> printfn "connection '%s', update of '%A' failed:\n %s" connString post e.Message