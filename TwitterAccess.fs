namespace DataAccess

open System.IO
open NPoco
open Microsoft.Data.Sqlite

open DataAccess.Types

module TwitterAccess =

    let private connString = "Filename=" + Path.Combine(Directory.GetCurrentDirectory(), "Sample.db")
    let private insertLeaderCmd = sprintf @"
insert into twitter_leader (%s) values ($UserScreenName, $Active)" LeaderRecordColumns
    let private insertLeaderFeedCmd = sprintf @"
insert into twitter_leader_feeds (%s) 
values ($UserScreenName, $TweetId, $Status, $TweetedAt, null)" FeedRecordColumns //"user_screen_name,tweet_id,status,tweeted_at,gabbed_at"
    let private selectActiveLeadersCmd = "select user_screen_name from twitter_leader where active is 1"
    let private selectFeedChunkCmd user_screen_name = sprintf @"
select * from twitter_leader_feeds 
where gabbed_at is null AND UPPER(user_screen_name) like '%s%%' limit %d" user_screen_name Globals.TwitterFeedChunkSize
    let private updateFeedChunkRecordCmd = sprintf @"
update twitter_leader_feeds set gabbed_at=$GabbedAt
where user_screen_name=$UserScreenName and tweet_id=$TweetId"

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


    // select * from twitter where active is 1;
    let selectActiveLeaders () = 
        try
            use conn = new SqliteConnection(connString)
            conn.Open()
            use db = new Database(conn)
            db.Fetch<LeaderRecord>(selectActiveLeadersCmd) |> List.ofSeq
        with _ as e -> printfn "connection '%s', active Twitter leaders select failed:\n %s" connString e.Message; []   


    let selectUserFeedChunk (user_screen_name : string) = 
        try
            use conn = new SqliteConnection(connString)
            conn.Open()
            use db = new Database(conn)
            db.Fetch<FeedRecord>(selectFeedChunkCmd (user_screen_name.ToUpper())) |> List.ofSeq
        with _ as e -> printfn "connection '%s', active Twitter leaders select failed:\n %s" connString e.Message; []   


    let selectTweetsFeedChunk () =
        selectActiveLeaders ()
        |> Seq.map (fun l -> l.UserScreenName |> selectUserFeedChunk)
        |> Array.ofSeq
        |> fun x -> Utils.shuffle x; x
        |> Seq.concat
            

    let updateUserChunkFeedRecord (feedRecord : FeedRecord) = 
        try
            use conn = new SqliteConnection(connString)
            conn.Open()        
            use txn: SqliteTransaction = conn.BeginTransaction()
            let cmd = conn.CreateCommand()
            cmd.Transaction <- txn
            cmd.CommandText <- updateFeedChunkRecordCmd
            cmd.Parameters.AddWithValue("$UserScreenName", feedRecord.UserScreenName) |> ignore
            cmd.Parameters.AddWithValue("$TweetId",        feedRecord.TweetId)        |> ignore
            cmd.Parameters.AddWithValue("$GabbedAt",       feedRecord.GabbedAt)       |> ignore
            cmd.ExecuteNonQuery() |> ignore
            txn.Commit()
            let result = sprintf """{  "msg": "%s,'%s' added to Twitter leader feed" } """ 
                            feedRecord.UserScreenName feedRecord.TweetId
            printfn "%s" result
            result
        with _ as e -> sprintf """{  "error_msg": "connection '%s', update of user feed '%s','%s' failed:\n %s" }""" 
                            connString feedRecord.UserScreenName feedRecord.TweetId e.Message        