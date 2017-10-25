CREATE TABLE gab (
actuser_name TEXT NOT NULL,
post_id TEXT NOT NULL,
post_body TEXT NOT NULL,
post_created_at TEXT NOT NULL,
thumbnail_created_at TEXT,
tweeted_at TEXT, 
media_id TEXT);
CREATE UNIQUE INDEX idx_posts on gab (actuser_name, post_id);
CREATE TABLE twitter_leader (
  user_screen_name TEXT NOT NULL, 
  active BOOL NOT NULL);
CREATE UNIQUE INDEX idx_leaders on twitter_leader (user_screen_name);
CREATE TABLE twitter_leader_feeds  (
  user_screen_name TEXT NOT NULL, 
  tweet_id TEXT NOT NULL, 
  status TEXT, 
  tweeted_at TEXT NOT NULL, 
  gabbed_at TEXT);
CREATE UNIQUE INDEX idx_tweets on twitter_leader_feeds (user_screen_name, tweet_id);
/* No STAT tables available */