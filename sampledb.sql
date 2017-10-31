CREATE TABLE gab (
	actuser_name TEXT NOT NULL,
	post_id TEXT NOT NULL,
	post_body TEXT NOT NULL,
	post_created_at CHAR(25) NOT NULL,
	thumbnail_created_at CHAR(25),
	tweeted_at CHAR(25), 
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
	tweeted_at CHAR(25) NOT NULL, 
	gabbed_at CHAR(25));
CREATE UNIQUE INDEX idx_tweets on twitter_leader_feeds (user_screen_name, tweet_id);
CREATE TABLE gabjwt (
	secretHash CHAR(64) PRIMARY KEY,
	jwtHeader CHAR(36) NOT NULL, 
	jwtPayload CHAR(158) NOT NULL, 
	jwtSignature CHAR(43) NOT NULL, 
	exp CHAR(25) NOT NULL);
/* No STAT tables available */