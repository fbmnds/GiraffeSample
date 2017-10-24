module GabaiTypes

type PostRecord() =
    member val ActuserName   = "" with get, set
    member val PostId        = "" with get, set
    member val PostBody      = "" with get, set
    member val PostCreatedAt = "" with get, set
    member val MediaId       = "" with get, set

let PostRecordColumns = "actuser_name,post_id,post_body,post_created_at,media_id"