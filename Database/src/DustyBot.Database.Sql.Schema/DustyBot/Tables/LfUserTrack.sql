CREATE TABLE [DustyBot].[LfUserTrack]
(
	[Id] int IDENTITY(1 ,1) NOT NULL,
	[UserId] int NOT NULL,
	[TrackId] int NOT NULL,
	[Plays] int NOT NULL,
	[Modified] datetime NOT NULL,
	CONSTRAINT [PK_LfUserTrack_Id] PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON),
    CONSTRAINT [FK_LfmUserTrack_LfUser] FOREIGN KEY ([UserId]) REFERENCES [DustyBot].[LfUser]([Id]), 
    CONSTRAINT [FK_LfmUserTrack_LfTrack] FOREIGN KEY ([TrackId]) REFERENCES [DustyBot].[LfTrack]([Id])
)
