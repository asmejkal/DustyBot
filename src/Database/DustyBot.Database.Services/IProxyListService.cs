using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Services
{
    public interface IProxyListService
    {
        Task<IReadOnlyCollection<ProxyBlacklistItem>> GetBlacklistAsync();
        Task BlacklistProxyAsync(string address, TimeSpan duration);
    }
}
