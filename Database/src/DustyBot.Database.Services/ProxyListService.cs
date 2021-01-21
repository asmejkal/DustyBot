using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DustyBot.Core.Collections;
using DustyBot.Core.Miscellaneous;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Services
{
    public class ProxyListService : IProxyListService
    {
        private readonly ISettingsService _settings;
        private readonly ITimeProvider _timeProvider;

        public ProxyListService(ISettingsService settings, ITimeProvider timeProvider)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _timeProvider = timeProvider;
        }

        public Task BlacklistProxyAsync(string address, TimeSpan duration)
        {
            var item = new ProxyBlacklistItem() { Address = address, Expiration = _timeProvider.Now + duration };
            return _settings.ModifyGlobal<ProxyList>(x => x.Blacklist[address] = item);
        }

        public async Task<IReadOnlyCollection<ProxyBlacklistItem>> GetBlacklistAsync()
        {
            return await _settings.ModifyGlobal((ProxyList x) =>
            {
                var now = _timeProvider.Now;
                x.Blacklist.RemoveAll((k, v) => v.Expiration < now);
                return x.Blacklist.Values;
            });
        }
    }
}
