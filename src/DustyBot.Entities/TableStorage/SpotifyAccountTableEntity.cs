using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Entities.TableStorage
{
    public class SpotifyAccountTableEntity : TableEntity
    {
        public string UserId { get; set; }
        public string RefreshToken { get; set; }
    }
}
