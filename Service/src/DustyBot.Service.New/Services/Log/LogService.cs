using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using DustyBot.Database.Mongo.Collections.Log;
using DustyBot.Database.Services;
using DustyBot.Framework.Client;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Services;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Services.Log
{
    internal class LogService : DustyBotService, ILogService
    {
        private readonly ISettingsService _settings;
        private readonly ILogSender _sender;

        public LogService(ISettingsService settings, ILogSender sender)
            : base()
        {
            _settings = settings;
            _sender = sender;
        }

        public Task EnableMessageLoggingAsync(Snowflake guildId, IMessageGuildChannel channel, CancellationToken ct)
        {
            return _settings.Modify(guildId, (LogSettings s) => s.MessageDeletedChannel = channel.Id, ct);
        }

        public Task DisableMessageLoggingAsync(Snowflake guildId, CancellationToken ct)
        {
            return _settings.Modify(guildId, (LogSettings s) => s.MessageDeletedChannel = default, ct);
        }

        public Task AddPrefixFilterAsync(Snowflake guildId, string prefix, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(prefix))
                throw new ArgumentException("Null or empty", nameof(prefix));

            return _settings.Modify(guildId, (LogSettings s) => s.MessageDeletedPrefixFilters.Add(prefix), ct);
        }

        public Task<bool> RemovePrefixFilterAsync(Snowflake guildId, string prefix, CancellationToken ct)
        {
            return _settings.Modify(guildId, (LogSettings s) => s.MessageDeletedPrefixFilters.Remove(prefix), ct);
        }

        public Task ClearPrefixFiltersAsync(Snowflake guildId, CancellationToken ct)
        {
            return _settings.Modify(guildId, (LogSettings s) => s.MessageDeletedPrefixFilters.Clear(), ct);
        }

        public async Task<IEnumerable<string>> GetPrefixFiltersAsync(Snowflake guildId, CancellationToken ct)
        {
            var settings = await _settings.Read<LogSettings>(guildId, false, ct);
            return settings?.MessageDeletedPrefixFilters ?? Enumerable.Empty<string>();
        }

        public Task AddChannelFilterAsync(Snowflake guildId, IEnumerable<IMessageGuildChannel> channels, CancellationToken ct)
        {
            return _settings.Modify(guildId, (LogSettings s) => s.MessageDeletedChannelFilters.UnionWith(channels.Select(x => (ulong)x.Id)), ct);
        }

        public Task RemoveChannelFilterAsync(Snowflake guildId, IEnumerable<IMessageGuildChannel> channels, CancellationToken ct)
        {
            return _settings.Modify(guildId, (LogSettings s) => s.MessageDeletedChannelFilters.ExceptWith(channels.Select(x => (ulong)x.Id)), ct);
        }

        public async Task<IEnumerable<ulong>> GetChannelFiltersAsync(Snowflake guildId, CancellationToken ct)
        {
            var settings = await _settings.Read<LogSettings>(guildId, false, ct);
            return settings?.MessageDeletedChannelFilters ?? Enumerable.Empty<ulong>();
        }

        protected override async ValueTask OnMessageDeleted(MessageDeletedEventArgs e)
        {
            try
            {
                if (e.GuildId == null || e.Message == null || e.Message.Author.IsBot)
                    return;

                var settings = await _settings.Read<LogSettings>(e.GuildId.Value, false, Bot.StoppingToken);
                if (settings == null)
                    return;

                var eventChannelId = settings.MessageDeletedChannel;
                if (eventChannelId == default)
                    return;

                var guild = Bot.GetGuildOrThrow(e.GuildId.Value);
                var channel = (IMessageGuildChannel)guild.GetChannelOrThrow(e.ChannelId);

                if (channel.Type == ChannelType.PrivateThread && channel is IThreadChannel thread && thread.CurrentMember == null)
                    return; // Only log messages from private threads if the bot was invited

                if (settings.MessageDeletedChannelFilters.Contains(channel.Id))
                    return;

                var eventChannel = guild.GetChannel(eventChannelId) as IMessageGuildChannel;
                if (eventChannel == null)
                    return;

                using var scope = Logger.WithArgs(e).WithGuild(guild).WithChannel(channel).BeginScope();
                if (!guild.GetBotPermissions(channel).SendEmbeds)
                {
                    Logger.LogInformation("Can't log deleted message because of missing permissions");
                    return;
                }

                if (settings.MessageDeletedPrefixFilters.Any(x => e.Message.Content.StartsWith(x)))
                    return;

                if ((await Bot.FindCommandsAsync(e.Message)).Any())
                    return;

                Logger.LogTrace("Logging a deleted message");
                await _sender.SendDeletedMessageLogAsync(eventChannel, e.Message, channel, Bot.StoppingToken);
            }
            catch (Exception ex)
            {
                Logger.WithArgs(e).LogError(ex, "Failed to process deleted messages");
            }
        }

        protected override async ValueTask OnMessagesDeleted(MessagesDeletedEventArgs e)
        {
            try
            {
                var messages = e.Messages
                    .Select(x => x.Value)
                    .Where(x => x != null && !x.Author.IsBot)
                    .ToList();

                if (!messages.Any())
                    return;

                var settings = await _settings.Read<LogSettings>(e.GuildId, false, Bot.StoppingToken);
                if (settings == null)
                    return;

                var eventChannelId = settings.MessageDeletedChannel;
                if (eventChannelId == default)
                    return;

                var guild = Bot.GetGuildOrThrow(e.GuildId);
                var channel = (IMessageGuildChannel)guild.GetChannelOrThrow(e.ChannelId);

                if (channel.Type == ChannelType.PrivateThread && channel is IThreadChannel thread && thread.CurrentMember == null)
                    return; // Only log messages from private threads if the bot was invited

                if (settings.MessageDeletedChannelFilters.Contains(channel.Id))
                    return;

                var eventChannel = guild.GetChannel(eventChannelId) as IMessageGuildChannel;
                if (eventChannel == null)
                    return;

                using var scope = Logger.WithGuild(guild).WithChannel(channel).BeginScope();
                if (!guild.GetBotPermissions(channel).SendEmbeds)
                {
                    Logger.LogInformation("Can't log bulk deleted messages because of missing permissions");
                    return;
                }

                Logger.LogTrace("Logging {Count} deleted messages", messages.Count);
                await _sender.SendDeletedMessageLogsAsync(eventChannel, messages, channel, Bot.StoppingToken);
            }
            catch (Exception ex)
            {
                Logger.WithArgs(e).LogError(ex, "Failed to process deleted messages");
            }
        }
    }
}
