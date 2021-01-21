using System;
using DustyBot.Core.Formatting;

namespace DustyBot.Database.Sql.UserDefinedTypes
{
    public class SetUserTracksTable
    {
        public const string TypeName = "[DustyBot].[SetUserTracksTable]";

        public string Username { get; }
        public int Plays { get; }
        public string LfId { get; }
        public string Name { get; }
        public string Url { get; }
        public string ArtistLfId { get; }
        public string ArtistName { get; }
        public string ArtistUrl { get; }

        public SetUserTracksTable(string username, int plays, string lfId, string name, string url, string artistLfId, string artistName, string artistUrl)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Plays = plays;
            LfId = lfId ?? throw new ArgumentNullException(nameof(lfId));
            Name = (name ?? throw new ArgumentNullException(nameof(name))).Truncate(200);
            Url = url ?? throw new ArgumentNullException(nameof(url));
            ArtistLfId = artistLfId ?? throw new ArgumentNullException(nameof(artistLfId));
            ArtistName = (artistName ?? throw new ArgumentNullException(nameof(artistName))).Truncate(200);
            ArtistUrl = artistUrl ?? throw new ArgumentNullException(nameof(artistUrl));
        }
    }
}
