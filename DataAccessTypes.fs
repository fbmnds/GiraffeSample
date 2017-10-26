namespace DataAccess

module Types =

    type Bool =
    | True = 1
    | False = 0

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


    type GabPost () = 
        member val body                = ""      with get, set
        member val reply_to            = ""      with get, set
        member val is_quote            = "0"     with get, set
        member val nsfw                = "0"     with get, set
        member val _method             = "post"  with get, set
        member val gif                 = ""      with get, set
        member val category            = "null"  with get, set
        member val topic               = "ef42509b-f3ab-4c1c-ba97-debb43d4f704"     
                                                 with get, set    
        member val share_facebook      = "null"  with get, set
        member val share_twitter       = "null"  with get, set
        member val is_replies_disabled = "false" with get, set
        member val media_attachments   = "[]"    with get, set

    let GabPostEncoded (post : GabPost) =
        let data =
            sprintf """
{
  "body": "%s",
  "reply_to": "%s",
  "is_quote": "%s",
  "nsfw": "%s",
  "_method": "%s",
  "gif": "%s",
  "category": %s,
  "topic": "%s",
  "share_facebook": %s,
  "share_twitter": %s,
  "is_replies_disabled": %s,
  "media_attachments": %s
}
""" 
        data
            (Utils.javascriptEncode post.body)
            post.reply_to
            post.is_quote
            post.nsfw
            post._method
            post.gif
            post.category
            post.topic
            post.share_facebook
            post.share_twitter
            post.is_replies_disabled
            post.media_attachments
