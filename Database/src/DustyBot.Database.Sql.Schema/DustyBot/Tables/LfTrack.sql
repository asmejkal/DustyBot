CREATE TABLE [DustyBot].[LfTrack]
(
	[Id] int IDENTITY(1, 1) NOT NULL,
	[LfId] char(64) UNIQUE NOT NULL,
	[Name] nvarchar(200) NOT NULL,
	[Url] nvarchar(max) NOT NULL,
	[ArtistId] int NOT NULL, 
	[Modified] datetime NOT NULL,
	CONSTRAINT [PK_LfTrack_Id] PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON),
    CONSTRAINT [FK_LfTrack_LfArtist] FOREIGN KEY ([ArtistId]) REFERENCES [DustyBot].[LfArtist]([Id])
)