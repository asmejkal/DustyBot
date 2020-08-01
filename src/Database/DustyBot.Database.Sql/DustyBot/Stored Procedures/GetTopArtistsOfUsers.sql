CREATE PROCEDURE [DustyBot].[GetTopArtistsOfUsers]
	@users [DustyBot].[GetTopArtistsOfUsersTable] READONLY,
	@count int
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	SELECT TOP(@count)
		a.[Name] AS [Name],
		a.[Url] AS [Url],
		SUM(ut.[Plays]) AS [Plays]	
	FROM 
		@users i
		JOIN [DustyBot].[LfUser] u ON u.Username = i.Username
		JOIN [DustyBot].[LfUserTrack] ut ON ut.[UserId] = u.[Id]
		JOIN [DustyBot].[LfTrack] t ON t.[Id] = ut.[TrackId]
		JOIN [DustyBot].[LfArtist] a ON a.[Id] = t.[ArtistId]
	GROUP BY a.[Name], a.[Url]
	ORDER BY SUM(ut.[Plays]) DESC;

END
