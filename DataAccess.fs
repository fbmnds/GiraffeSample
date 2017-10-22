namespace DataAccess

open System.IO
open NPoco
open Microsoft.Data.Sqlite

open LunchTypes
open GabaiTypes

module GabaiAccess =
    let private connString = "Filename=" + Path.Combine(Directory.GetCurrentDirectory(), "Sample.db")
    let private insertCmd =  @"
insert into gab (actuser_name, post_id, post_body, post_created_at)
values ($ActuserName, $PostId, $PostBody, $PostCreatedAt)"
    let updateCmdThumbnail =  @"
update gab set thumbnail_created_at=$TimeStamp
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
        let query = sprintf "select actuser_name,post_id from gab where %s" clause
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
            cmd.Parameters.AddWithValue("$TimeStamp",    System.DateTime.Now.ToString()) |> ignore
            cmd.ExecuteNonQuery() |> ignore
            txn.Commit()            
        with _ as e -> printfn "connection '%s', update of '%A' failed:\n %s" connString post e.Message



module LunchAccess =
    let private connString = "Filename=" + Path.Combine(Directory.GetCurrentDirectory(), "Sample.db")

    let delLunch (lunchID: LunchID) =
        use conn = new SqliteConnection(connString)
        conn.Open()
        
        use txn: SqliteTransaction = conn.BeginTransaction()

        let cmd = conn.CreateCommand()
        cmd.Transaction <- txn
        cmd.CommandText <- @"delete from LunchSpots where ID = $ID"
        cmd.Parameters.AddWithValue("$ID", lunchID.ID) |> ignore

        cmd.ExecuteNonQuery() |> ignore
        
        txn.Commit()

    let addLunch (lunchSpot: LunchSpot) =
        use conn = new SqliteConnection(connString)
        conn.Open()
        
        use txn: SqliteTransaction = conn.BeginTransaction()

        let cmd = conn.CreateCommand()
        cmd.Transaction <- txn
        cmd.CommandText <- @"
insert into LunchSpots (Name, Latitude, Longitude, Cuisine, VegetarianOptions, VeganOptions)
values ($Name, $Latitude, $Longitude, $Cuisine, $VegetarianOptions, $VeganOptions)"

        cmd.Parameters.AddWithValue("$Name", lunchSpot.Name) |> ignore
        cmd.Parameters.AddWithValue("$Latitude", lunchSpot.Latitude) |> ignore
        cmd.Parameters.AddWithValue("$Longitude", lunchSpot.Longitude) |> ignore
        cmd.Parameters.AddWithValue("$Cuisine", lunchSpot.Cuisine) |> ignore
        cmd.Parameters.AddWithValue("$VegetarianOptions", lunchSpot.VegetarianOptions) |> ignore
        cmd.Parameters.AddWithValue("$VeganOptions", lunchSpot.VeganOptions) |> ignore

        cmd.ExecuteNonQuery() |> ignore

        txn.Commit()

    let private getLunchFetchingQuery filter =
        let cuisinePart, hasCuisine =
            match filter.Cuisine with
            | Some c -> (sprintf "Cuisine = \"%s\"" c, true)
            | None -> ("", false)

        let vegetarianPart, hasVegetarianPart =
            match filter.VegetarianOptions with
            | Some v -> (sprintf "VegetarianOptions = \"%d\"" (if v then 1 else 0), true) // Sqlite uses ints 0 and 1 for bools.
            | None -> ("", false)

        let veganPart, hasVeganPart =
            match filter.VeganOptions with
            | Some v -> (sprintf "VeganOptions = \"%d\"" (if v then 1 else 0), true) // Sqlite uses ints 0 and 1 for bools.
            | None -> ("", false)

        let hasWhereClause = hasCuisine || hasVegetarianPart || hasVeganPart

        let query = 
            "select * from LunchSpots" + 
            (if hasWhereClause then " where " else "") +
            cuisinePart +
            (if hasCuisine && hasVegetarianPart then " and " + vegetarianPart else vegetarianPart) +
            (if (hasCuisine || hasVegetarianPart) && hasVeganPart then " and " + veganPart else veganPart)

        query

    let getLunches (filter: LunchFilter) =
        let query = getLunchFetchingQuery filter

        use conn = new SqliteConnection(connString)
        conn.Open()

        use db = new Database(conn)
        db.Fetch<LunchSpot>(query)