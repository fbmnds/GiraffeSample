
CREATE TABLE IF NOT EXISTS "LunchSpots" (
  "Id" integer PRIMARY KEY AUTOINCREMENT NOT NULL,
    "Name" nvarchar(128) NOT NULL,
      "Latitude" double(128) NOT NULL,
        "Longitude" double(128) NOT NULL,
	  "Cuisine" nvarchar(128) NOT NULL,
	    "VegetarianOptions" integer(128) NOT NULL,
	      "VeganOptions" integer(128) NOT NULL
	      );
