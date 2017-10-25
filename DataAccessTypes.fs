module DataAccess.Types

type PostRecord () =
    member val ActuserName   = "" with get, set
    member val PostId        = "" with get, set
    member val PostBody      = "" with get, set
    member val PostCreatedAt = "" with get, set
    member val MediaId       = "" with get, set

let PostRecordColumns = "actuser_name,post_id,post_body,post_created_at,media_id"


type LeaderRecord () =
    member val UserScreenName = ""   with get, set
    member val active         = true with get, set

let LeaderRecordColumns = "user_screen_name,active"


type FeedRecord () =
    member val UserScreenName = "" with get, set
    member val TweetId        = "" with get, set
    member val Status         = "" with get, set
    member val TweetedAt      = "" with get, set
    member val GabbedAt       = "" with get, set

let FeedRecordColumns = "user_screen_name,tweet_id,status,tweeted_at,gabbed_at"