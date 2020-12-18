using DustyBot.Database.Services;
using DustyBot.Framework.Configuration;
using DustyBot.Settings;
using System;
using System.Threading.Tasks;

namespace DustyBot.Config
{
    public class FrameworkGuildConfigProvider : IFrameworkGuildConfigProvider
    {
        private ISettingsService _settingsService;

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
