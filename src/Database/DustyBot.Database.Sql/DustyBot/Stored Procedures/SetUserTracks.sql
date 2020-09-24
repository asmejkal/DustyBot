CREATE PROCEDURE [DustyBot].[SetUserTracks]
	@tracks [DustyBot].[SetUserTracksTable] READONLY
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRAN;

    DECLARE @now datetime;
    SELECT @now = SYSUTCDATETIME();
    
    WITH T AS 
    (
        SELECT 
            [ArtistLfId] AS [LfId],
            FIRST([ArtistName]) AS [Name],
            FIRST([ArtistUrl]) AS [Url]
        FROM @tracks
        GROUP BY [ArtistLfId]
    )
    MERGE INTO [DustyBot].[LfArtist] AS [target]
    USING T AS [source]
    ON [target].[LfId] = [source].[LfId]
    WHEN MATCHED
        THEN UPDATE SET
            [Name] = [source].[Name],
            [Url] = [source].[Url],
            [Modified] = @now
    WHEN NOT MATCHED
        THEN INSERT
        (
            [LfId],
            [Name],
            [Url],
            [Modified]
        )
        VALUES
        (
            [source].[LfId],
            [source].[Name],
            [source].[Url],
            @now
        );

    WITH T AS
    (
        SELECT
            t.*,
            a.[Id] AS [ArtistId]
        FROM @tracks t
        JOIN [DustyBot].[LfArtist] a ON a.[LfId] = t.[ArtistLfId]
    )
	MERGE INTO [DustyBot].[LfTrack] AS [target]
    USING T AS [source]
    ON [target].[LfId] = [source].[LfId]
    WHEN MATCHED
        THEN UPDATE SET
            [Name] = [source].[Name],
            [Url] = [source].[Url],
            [ArtistId] = [source].[ArtistId],
            [Modified] = @now
    WHEN NOT MATCHED
        THEN INSERT
        (
            [LfId],
            [Name],
            [Url],
            [ArtistId],
            [Modified]
        )
        VALUES
        (
            [source].[LfId],
            [source].[Name],
            [source].[Url],
            [source].[ArtistId],
            @now
        );

    WITH T AS
    (
        SELECT DISTINCT
            [Username]
        FROM @tracks
    )
    MERGE INTO [DustyBot].[LfUser] AS [target]
    USING T AS [source]
    ON [target].[Username] = [source].[Username]
    WHEN NOT MATCHED
        THEN INSERT
        (
            [Username],
            [Modified]
        )
        VALUES
        (
            [source].[Username],
            @now
        );

    WITH T AS
    (
        SELECT
            u.[Id] AS [UserId],
            t.[Id] AS [TrackId],
            i.[Plays]
        FROM @tracks i
        JOIN [DustyBot].[LfTrack] t ON t.[LfId] = i.[LfId]
        JOIN [DustyBot].[LfUser] u ON u.[Username] = i.[Username]
    )
	MERGE INTO [DustyBot].[LfUserTrack] AS [target]
    USING T AS [source]
    ON [target].[TrackId] = [source].[TrackId] AND [target].[UserId] = [source].[UserId]
    WHEN MATCHED
        THEN UPDATE SET
            [Plays] = [source].[Plays],
            [Modified] = @now
    WHEN NOT MATCHED
        THEN INSERT
        (
            [UserId],
            [TrackId],
            [Plays],
            [Modified]
        )
        VALUES
        (
            [source].[UserId],
            [source].[TrackId],
            [source].[Plays],
            @now
        );

	COMMIT TRAN;

END