using System;

namespace DustyBot.Database.Sql.UserDefinedTypes
{
    public class GetTopArtistsOfUsersTable
    {
        public const string TypeName = "[DustyBot].[GetTopArtistsOfUsersTable]";

        public string Username { get; }

        public GetTopArtistsOfUsersTable(string username)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
        }
    }
}
