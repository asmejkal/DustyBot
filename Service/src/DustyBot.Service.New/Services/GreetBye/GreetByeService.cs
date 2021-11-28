using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Client;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Services;
using DustyBot.Service.Services.Automod;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Services.GreetBye
{
    // TODO: migrate the DB and remove all the ifs
    internal class GreetByeService : DustyBotService, IGreetByeService, IDisposable
    {
        private readonly IGreetByeMessageBuilder _messageBuilder;
        private readonly ISettingsService _settings;
        private readonly IAutomodService _automodService;

        private readonly ConcurrentDictionary<(Snowflake GuildId, Snowflake UserId), DateTimeOffset> _autobannedUsers = new();

        public override int Priority => _automodService.Priority - 1;

        public GreetByeService(IGreetByeMessageBuilder messageBuilder, ISettingsService settings, IAutomodService automodService)
            : base()
        {
            _messageBuilder = messageBuilder;
            _settings = settings;
            _automodService = automodService;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _automodService.UserAutobanned += HandleUserAutobanned;
            return Task.CompletedTask;
        }

        public Task SetEventTextAsync(GreetByeEventType type, Snowflake guildId, ITextChannel channel, string message)
        {
            return _settings.Modify(guildId, (EventsSettings s) =>
            {
                if (type == GreetByeEventType.Greet)
                {
                    s.ResetGreet();
                    s.GreetChannel = channel.Id;
                    s.GreetMessage = message;
                }
                else if (type == GreetByeEventType.Bye)
                {
                    s.ResetBye();
                    s.ByeChannel = channel.Id;
                    s.ByeMessage = message;
                }
            });
        }

        public Task SetEventEmbedAsync(GreetByeEventType type, Snowflake guildId, ITextChannel channel, Color? color, Uri? image, string title, string body)
        {
            return _settings.Modify(guildId, (EventsSettings s) =>
            {
                var embed = new GreetByeEmbed(title, body, image);
                if (color.HasValue)
                    embed.Color = (uint)color.Value.RawValue;

                if (type == GreetByeEventType.Greet)
                {
                    s.ResetGreet();
                    s.GreetChannel = channel.Id;
                    s.GreetEmbed = embed;
                }
                else if (type == GreetByeEventType.Bye)
                {
                    s.ResetBye();
                    s.ByeChannel = channel.Id;
                    s.ByeEmbed = embed;
                }
            });
        }

        public Task<SetEventEmbedFooterResult> SetEventEmbedFooterAsync(GreetByeEventType type, Snowflake guildId, string? footer)
        {
            return _settings.Modify(guildId, (EventsSettings s) =>
            {
                if (type == GreetByeEventType.Greet)
                {
                    if (s.GreetEmbed == null)
                        return SetEventEmbedFooterResult.EventEmbedNotSet;

                    s.GreetEmbed.Footer = !string.IsNullOrEmpty(footer) ? footer : null;
                }
                else if (type == GreetByeEventType.Bye)
                {
                    if (s.ByeEmbed == null)
                        return SetEventEmbedFooterResult.EventEmbedNotSet;

                    s.ByeEmbed.Footer = !string.IsNullOrEmpty(footer) ? footer : null;
                }
                
                return SetEventEmbedFooterResult.Success;
            });
        }

        public Task DisableEventAsync(GreetByeEventType type, Snowflake guildId)
        {
            return _settings.Modify(guildId, (EventsSettings s) =>
            {
                if (type == GreetByeEventType.Greet)
                    s.ResetGreet();
                else if (type == GreetByeEventType.Bye)
                    s.ResetBye();
            });
        }

        public async Task<TriggerEventResult> TriggerEventAsync(
            GreetByeEventType type, 
            IGatewayGuild guild, 
            IMessageGuildChannel channel, 
            IUser user)
        {
            var settings = await _settings.Read<EventsSettings>(guild.Id, false);
            return await TriggerEventAsync(type, guild, channel.Id, user, settings);
        }

        public override void Dispose()
        {
            _automodService.UserAutobanned -= HandleUserAutobanned;
            base.Dispose();
        }

        protected override async ValueTask OnMemberJoined(MemberJoinedEventArgs e)
        {
            try
            {
                var settings = await _settings.Read<EventsSettings>(e.GuildId, false);
                if (settings == null || settings.GreetChannel == default)
                    return;

                if (_autobannedUsers.ContainsKey((e.GuildId, e.MemberId)))
                    return;

                var guild = Bot.GetGuildOrThrow(e.GuildId);
                var channel = guild.GetChannelOrThrow(settings.GreetChannel);

                using var scope = Logger.BuildScope(x => x.WithArgs(e).WithGuild(guild).WithChannel(channel));
                if (!guild.GetBotPermissions(channel).SendMessages)
                {
                    Logger.LogInformation("Can't greet user because of missing permissions");
                    return;
                }

                Logger.LogInformation("Greeting user");
                await TriggerEventAsync(GreetByeEventType.Greet, guild, channel.Id, e.Member, settings);
            }
            catch (Exception ex)
            {
                Logger.WithScope(x => x.WithArgs(e)).LogError(ex, "Failed to process greeting");
            }
        }

        protected override async ValueTask OnMemberLeft(MemberLeftEventArgs e)
        {
            try
            {
                var settings = await _settings.Read<EventsSettings>(e.GuildId, false);
                if (settings == null || settings.ByeChannel == default)
                    return;

                if (_autobannedUsers.ContainsKey((e.GuildId, e.MemberId)))
                    return; // TODO: possible race condition

                var guild = Bot.GetGuildOrThrow(e.GuildId);
                var channel = guild.GetChannelOrThrow(settings.ByeChannel);

                using var scope = Logger.BuildScope(x => x.WithArgs(e).WithGuild(guild).WithChannel(channel));
                if (!guild.GetBotPermissions(channel).SendMessages)
                {
                    Logger.LogInformation("Can't bye user because of missing permissions");
                    return;
                }

                Logger.LogInformation("Saying goodbye to user");
                await TriggerEventAsync(GreetByeEventType.Bye, guild, channel.Id, e.User, settings);
            }
            catch (Exception ex)
            {
                Logger.WithScope(x => x.WithArgs(e)).LogError(ex, "Failed to process bye event");
            }
        }

        private void HandleUserAutobanned(object? sender, (Snowflake GuildId, Snowflake UserId) e)
        {
            var now = DateTimeOffset.UtcNow;
            _autobannedUsers.AddOrUpdate(e, now, (x, y) => now);

            var pruneThreshold = now - TimeSpan.FromHours(1);
            foreach (var item in _autobannedUsers.ToList().Where(x => x.Value < pruneThreshold))
                _autobannedUsers.TryRemove(item.Key, out _);
        }

        private async Task<TriggerEventResult> TriggerEventAsync(
            GreetByeEventType type, 
            IGatewayGuild guild, 
            Snowflake channelId, 
            IUser user, 
            EventsSettings settings)
        {
            if (settings == null)
                return TriggerEventResult.EventNotSet;

            var message = new LocalMessage();
            if (type == GreetByeEventType.Greet)
            {
                if (settings.GreetMessage != default)
                    message.WithContent(_messageBuilder.BuildText(settings.GreetMessage, user, guild));
                else if (settings.GreetEmbed != default)
                    message.WithEmbeds(_messageBuilder.BuildEmbed(settings.GreetEmbed, user, guild));
                else
                    return TriggerEventResult.EventNotSet;
            }
            else if (type == GreetByeEventType.Bye)
            {
                if (settings.ByeMessage != default)
                    message.WithContent(_messageBuilder.BuildText(settings.ByeMessage, user, guild));
                else if (settings.ByeEmbed != default)
                    message.WithEmbeds(_messageBuilder.BuildEmbed(settings.ByeEmbed, user, guild));
                else
                    return TriggerEventResult.EventNotSet;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown enum value");
            }

            await Bot.SendMessageAsync(channelId, message);
            return TriggerEventResult.Success;
        }
    }
}
