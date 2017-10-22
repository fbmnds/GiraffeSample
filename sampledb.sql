CREATE TABLE "LunchSpots" (
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
  "Name" nvarchar(128) NOT NULL,
  "Latitude" double(128) NOT NULL,
  "Longitude" double(128) NOT NULL,
  "Cuisine" nvarchar(128) NOT NULL,
  "VegetarianOptions" integer(128) NOT NULL,
  "VeganOptions" integer(128) NOT NULL
);
CREATE TABLE gab (
	actuser_name TEXT NOT NULL, 
	post_id TEXT NOT NULL, 
	post_body TEXT NOT NULL, 
	post_created_at TEXT NOT NULL
);
CREATE UNIQUE INDEX idx_posts on gab (actuser_name, post_id);
/* No STAT tables available */