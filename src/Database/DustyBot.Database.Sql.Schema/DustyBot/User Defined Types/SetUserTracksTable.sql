CREATE TYPE [DustyBot].[SetUserTracksTable] AS TABLE
(
	[Username] nvarchar(100) NOT NULL,
	[Plays] int NOT NULL,
	[LfId] char(64) NOT NULL,
	[Name] nvarchar(200) NOT NULL,
	[Url] nvarchar(max) NOT NULL,
	[ArtistLfId] char(64) NOT NULL,
	[ArtistName] nvarchar(200) NOT NULL,
	[ArtistUrl] nvarchar(max) NOT NULL
)
