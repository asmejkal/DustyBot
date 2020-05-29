using System;
using System.Collections.Generic;
using System.Text;

namespace DustyBot.Entities.TableStorage
{
    public static class TableMappingExtensions
    {
        public static SpotifyAccountTableEntity ToTableEntity(this SpotifyAccount x, string etag = "*")
        {
            return new SpotifyAccountTableEntity()
            {
                PartitionKey = "root",
                RowKey = x.UserId.ToString(),
                ETag = etag,
                UserId = x.UserId.ToString(),
                RefreshToken = x.RefreshToken
            };
        }

        public static SpotifyAccount ToModel(this SpotifyAccountTableEntity x)
        {
            return new SpotifyAccount()
            {
                UserId = ulong.Parse(x.UserId),
                RefreshToken = x.RefreshToken
            };
        }
    }
}
