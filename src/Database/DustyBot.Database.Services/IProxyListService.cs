using DustyBot.Database.Mongo.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DustyBot.Database.Services
{
    public interface IProxyListService
    {
        Task<IReadOnlyCollection<ProxyBlacklistItem>> GetBlacklistAsync();
        Task BlacklistProxyAsync(string address, TimeSpan duration);
    }
}
