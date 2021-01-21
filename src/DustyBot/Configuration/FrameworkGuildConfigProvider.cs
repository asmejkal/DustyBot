using System;
using System.Threading.Tasks;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Services;
using DustyBot.Framework.Configuration;

namespace DustyBot.Configuration
{
    public class FrameworkGuildConfigProvider : IFrameworkGuildConfigProvider
    {
        private readonly ISettingsService _settingsService;

        public FrameworkGuildConfigProvider(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public async Task<FrameworkGuildConfig> GetConfigAsync(ulong guildId)
        {
            var settings = await _settingsService.Read<BotSettings>(guildId, createIfNeeded: false);
            return new FrameworkGuildConfig(settings?.CommandPrefix);
        }
    }
}
