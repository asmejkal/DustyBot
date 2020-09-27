using Microsoft.WindowsAzure.Storage.Table;

namespace DustyBot.Database.TableStorage.Tables
{
    public class SpotifyAccount : TableEntity
    {
        public const string TableName = "SpotifyAccounts";

        public string UserId { get; set; }
        public string RefreshToken { get; set; }
    }
}
