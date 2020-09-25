CREATE PROCEDURE [DustyBot].[GetTopArtists]
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
		[DustyBot].[LfUserTrack] ut
		JOIN [DustyBot].[LfTrack] t ON t.[Id] = ut.[TrackId]
		JOIN [DustyBot].[LfArtist] a ON a.[Id] = t.[ArtistId]
	GROUP BY a.[Name], a.[Url]
	ORDER BY SUM(ut.[Plays]) DESC;

END
