using DustyBot.Core.Collections;
using DustyBot.Database.Mongo.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Database.Services
{
    public class ProxyListService : IProxyListService
    {
        private ISettingsService _settings;

        public ProxyListService(ISettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public Task BlacklistProxyAsync(string address, TimeSpan duration)
        {
            var item = new ProxyBlacklistItem() { Address = address, Expiration = DateTimeOffset.Now + duration };
            return _settings.ModifyGlobal<ProxyList>(x => x.Blacklist[address] = item);
        }

        public async Task<IReadOnlyCollection<ProxyBlacklistItem>> GetBlacklistAsync()
        {
            return await _settings.ModifyGlobal((ProxyList x) =>
            {
                var now = DateTimeOffset.Now;
                x.Blacklist.RemoveAll((k, v) => v.Expiration < now);
                return x.Blacklist.Values;
            });
        }
    }
}
