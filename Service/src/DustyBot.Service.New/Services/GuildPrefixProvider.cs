using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Disqord.Bot;
using Disqord.Gateway;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Services;
using Microsoft.Extensions.Options;

namespace DustyBot.Service.Services
{
    internal class GuildPrefixProvider : IPrefixProvider
    {
        public IImmutableSet<IPrefix> DefaultPrefixes { get; set; }

        private readonly ISettingsService _settings;

        public GuildPrefixProvider(ISettingsService settings, IOptions<DefaultPrefixProviderConfiguration> options)
        {
            DefaultPrefixes = options.Value?.Prefixes?.ToImmutableHashSet() ?? ImmutableHashSet<IPrefix>.Empty;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async ValueTask<IEnumerable<IPrefix>> GetPrefixesAsync(IGatewayUserMessage message)
        {
            if (message.GuildId != null)
            {
                var settings = await _settings.Read<BotSettings>(message.GuildId.Value, false);
                if (!string.IsNullOrEmpty(settings?.CommandPrefix))
                    return new[] { new StringPrefix(settings.CommandPrefix) };
            }

            return DefaultPrefixes;
        }
    }
}
