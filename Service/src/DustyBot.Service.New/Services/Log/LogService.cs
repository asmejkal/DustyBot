using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using DustyBot.Core.Formatting;
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

        public LogService(ISettingsService settings)
            : base()
        {
            _settings = settings;
        }

        public Task EnableMessageLoggingAsync(Snowflake guildId, ITextChannel channel, CancellationToken ct)
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

        public Task RemovePrefixFilterAsync(Snowflake guildId, string prefix, CancellationToken ct)
        {
            return _settings.Modify(guildId, (LogSettings s) => s.MessageDeletedPrefixFilters.Remove(prefix), ct);
        }

        public async Task<IEnumerable<string>> GetPrefixFiltersAsync(Snowflake guildId, CancellationToken ct)
        {
            var settings = await _settings.Read<LogSettings>(guildId, false, ct);
            return settings?.MessageDeletedPrefixFilters ?? Enumerable.Empty<string>();
        }

        public Task AddChannelFilterAsync(Snowflake guildId, IEnumerable<ITextChannel> channels, CancellationToken ct)
        {
            return _settings.Modify(guildId, (LogSettings s) => s.MessageDeletedChannelFilters.UnionWith(channels.Select(x => (ulong)x.Id)), ct);
        }

        public Task RemoveChannelFilterAsync(Snowflake guildId, IEnumerable<ITextChannel> channels, CancellationToken ct)
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
                if (e.GuildId == null)
                    return;

                var guild = Bot.GetGuildOrThrow(e.GuildId.Value);
                var channel = (ITextChannel)guild.GetChannelOrThrow(e.ChannelId);

                if (e.Message.Author.IsBot)
                    return;

                var settings = await _settings.Read<LogSettings>(guild.Id, false, Bot.StoppingToken);
                if (settings == null)
                    return;

                var eventChannelId = settings.MessageDeletedChannel;
                if (eventChannelId == default)
                    return;

                if (settings.MessageDeletedChannelFilters.Contains(channel.Id))
                    return;

                var eventChannel = guild.GetTextChannel(eventChannelId);
                if (eventChannel == null)
                    return;

                using var scope = Logger.BuildScope(x => x.WithArgs(e).WithGuild(guild).WithChannel(channel));
                if (!guild.GetBotPermissions(channel).SendMessages)
                {
                    Logger.LogInformation("Can't log bulk deleted messages because of missing permissions");
                    return;
                }

                if (settings.MessageDeletedPrefixFilters.Any(x => e.Message.Content.StartsWith(x)))
                    return;

                Logger.LogInformation("Logging a deleted message");
                await LogSingleMessageAsync(eventChannel, channel, e.Message, Bot.StoppingToken);
            }
            catch (Exception ex)
            {
                Logger.WithScope(x => x.WithArgs(e)).LogError(ex, "Failed to process deleted messages");
            }
        }

        protected override async ValueTask OnMessagesDeleted(MessagesDeletedEventArgs e)
        {
            try
            {
                var guild = Bot.GetGuildOrThrow(e.GuildId);
                var channel = (ITextChannel)guild.GetChannelOrThrow(e.ChannelId);

                var messages = e.Messages
                    .Select(x => x.Value)
                    .Where(x => !x.Author.IsBot)
                    .ToList();

                if (!messages.Any())
                    return;

                var settings = await _settings.Read<LogSettings>(guild.Id, false, Bot.StoppingToken);
                if (settings == null)
                    return;

                var eventChannelId = settings.MessageDeletedChannel;
                if (eventChannelId == default)
                    return;

                if (settings.MessageDeletedChannelFilters.Contains(channel.Id))
                    return;

                var eventChannel = guild.GetTextChannel(eventChannelId);
                if (eventChannel == null)
                    return;

                using var scope = Logger.BuildScope(x => x.WithGuild(guild).WithChannel(channel));
                if (!guild.GetBotPermissions(channel).SendMessages)
                {
                    Logger.LogInformation("Can't log bulk deleted messages because of missing permissions");
                    return;
                }

                Logger.LogInformation("Logging {Count} deleted messages", messages.Count);

                var logs = new List<string>();
                foreach (var message in messages)
                {
                    if (settings.MessageDeletedPrefixFilters.Any(x => message.Content.StartsWith(x)))
                        continue;

                    var builder = new StringBuilder();
                    builder.AppendLine($"**Message by {message.Author.Mention} in {channel.Mention} was deleted:**");

                    if (!string.IsNullOrWhiteSpace(message.Content))
                        builder.AppendLine(message.Content);

                    if (message.Attachments.Any())
                        builder.AppendLine(string.Join("\n", message.Attachments.Select(a => a.Url)));

                    builder.Append(Markdown.Timestamp(message.CreatedAt()));

                    if (builder.Length > LocalEmbed.MaxDescriptionLength / 2)
                    {
                        try
                        {
                            await LogSingleMessageAsync(eventChannel, channel, message, Bot.StoppingToken);
                        }
                        catch (Exception ex)
                        {
                            Logger.WithScope(x => x.WithMessage(message)).LogError(ex, "Failed to log single deleted message in a bulk delete");
                        }

                        continue;
                    }
                    else
                    {
                        logs.Add(builder.ToString());
                    }
                }

                var embed = new LocalEmbed();
                var delimiter = "\n\n";
                foreach (var log in logs)
                {
                    var totalLength = log.Length + delimiter.Length;

                    if ((embed.Description?.Length ?? 0) + totalLength > LocalEmbed.MaxDescriptionLength)
                    {
                        await eventChannel.SendMessageAsync(new LocalMessage().WithEmbeds(embed));
                        embed = new LocalEmbed();
                    }
                    else
                    {
                        embed.Description += log + delimiter;
                    }
                }

                if (!string.IsNullOrEmpty(embed.Description))
                    await eventChannel.SendMessageAsync(new LocalMessage().WithEmbeds(embed));
            }
            catch (Exception ex)
            {
                Logger.WithScope(x => x.WithArgs(e)).LogError(ex, "Failed to process deleted messages");
            }
        }

        private static async Task LogSingleMessageAsync(ITextChannel eventChannel, ITextChannel channel, IUserMessage message, CancellationToken ct)
        {
            var preface = $"**Message by {message.Author.Mention} in {channel.Mention} was deleted:**\n";
            var embed = new LocalEmbed()
                .WithDescription(preface + message.Content.Truncate(LocalEmbed.MaxDescriptionLength - preface.Length))
                .WithTimestamp(message.CreatedAt());

            if (message.Attachments.Any())
                embed.AddField("Attachments", string.Join(", ", message.Attachments.Select(a => a.Url)));

            await eventChannel.SendMessageAsync(new LocalMessage().WithEmbeds(embed), cancellationToken: ct);
        }
    }
}
