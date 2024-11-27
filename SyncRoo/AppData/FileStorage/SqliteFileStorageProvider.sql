CREATE TABLE "PendingFile" (
	"Id"	INTEGER NOT NULL,
	"FileName"	TEXT NOT NULL,
	"Size"	INTEGER NOT NULL,
	"ModifiedTime"	INTEGER NOT NULL,
	PRIMARY KEY("Id" AUTOINCREMENT)
);

CREATE TABLE "SourceFile" (
	"FileName"	TEXT NOT NULL,
	"Size"	INTEGER NOT NULL,
	"ModifiedTime"	INTEGER NOT NULL,
	PRIMARY KEY("FileName")
);

CREATE TABLE "TargetFile" (
	"FileName"	TEXT NOT NULL,
	"Size"	INTEGER NOT NULL,
	"ModifiedTime"	INTEGER NOT NULL,
	PRIMARY KEY("FileName")
);