using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Database.Mongo.Collections.GreetBye;
using DustyBot.Database.Mongo.Collections.GreetBye.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Client;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Services;
using DustyBot.Service.Services.Automod;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Services.GreetBye
{
    internal class GreetByeService : DustyBotService, IGreetByeService, IDisposable
    {
        private readonly IGreetByeSender _sender;
        private readonly ISettingsService _settings;
        private readonly IAutomodService _automodService;

        private readonly ConcurrentDictionary<(Snowflake GuildId, Snowflake UserId), DateTimeOffset> _autobannedUsers = new();

        public override int Priority => _automodService.Priority - 1;

        public GreetByeService(IGreetByeSender sender, ISettingsService settings, IAutomodService automodService)
            : base()
        {
            _sender = sender;
            _settings = settings;
            _automodService = automodService;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _automodService.UserAutobanned += HandleUserAutobanned;
            return Task.CompletedTask;
        }

        public Task SetEventTextAsync(GreetByeEventType type, Snowflake guildId, ITextChannel channel, string message, CancellationToken ct)
        {
            return _settings.Modify(guildId, (GreetByeSettings s) => s.Events[type] = new(channel.Id, message), ct);
        }

        public Task SetEventEmbedAsync(
            GreetByeEventType type,
            Snowflake guildId,
            ITextChannel channel,
            string title,
            string body,
            Uri? image = null,
            Color? color = null,
            string? footer = null, 
            CancellationToken ct = default)
        {
            return _settings.Modify(guildId, 
                (GreetByeSettings s) => s.Events[type] = new(channel.Id, new GreetByeEmbed(title, body, image, color?.RawValue, footer)),
                ct);
        }

        public Task<UpdateEventEmbedFooterResult> UpdateEventEmbedFooterAsync(GreetByeEventType type, Snowflake guildId, string? footer, CancellationToken ct)
        {
            return _settings.Modify(guildId, (GreetByeSettings s) =>
            {
                if (!s.Events.TryGetValue(type, out var setting) || setting.Embed == null)
                    return UpdateEventEmbedFooterResult.EventEmbedNotSet;

                setting.Embed.Footer = footer;
                return UpdateEventEmbedFooterResult.Success;
            }, ct);
        }

        public Task DisableEventAsync(GreetByeEventType type, Snowflake guildId, CancellationToken ct)
        {
            return _settings.Modify(guildId, (GreetByeSettings s) => s.Events.Remove(type), ct);
        }

        public async Task<TriggerEventResult> TriggerEventAsync(
            GreetByeEventType type, 
            IGatewayGuild guild, 
            IMessageGuildChannel channel, 
            IUser user, 
            CancellationToken ct)
        {
            var settings = await _settings.Read<GreetByeSettings>(guild.Id, false);
            if (settings == null || !settings.Events.TryGetValue(type, out var setting))
                return TriggerEventResult.EventNotSet;

            return await TriggerEventAsync(setting, guild, channel, user, ct);
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
                using var scope = Logger.BuildScope(x => x.WithArgs(e));
                await HandleEventAsync(GreetByeEventType.Greet, e.GuildId, e.Member, Bot.StoppingToken);
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
                using var scope = Logger.BuildScope(x => x.WithArgs(e));
                await HandleEventAsync(GreetByeEventType.Bye, e.GuildId, e.User, Bot.StoppingToken);
            }
            catch (Exception ex)
            {
                Logger.WithScope(x => x.WithArgs(e)).LogError(ex, "Failed to process bye event");
            }
        }

        private async ValueTask HandleEventAsync(GreetByeEventType type, Snowflake guildId, IUser user, CancellationToken ct)
        {
            var settings = await _settings.Read<GreetByeSettings>(guildId, false, ct);
            if (settings == null || !settings.Events.TryGetValue(type, out var setting))
                return;

            if (_autobannedUsers.ContainsKey((guildId, user.Id)))
                return;

            var guild = Bot.GetGuildOrThrow(guildId);
            var channel = guild.GetChannel(setting.ChannelId) as IMessageGuildChannel;
            if (channel == null)
            {
                Logger.LogInformation("Can't send {GreetByeEventType} message because the channel was not found", type);
                return;
            }

            using var scope = Logger.BuildScope(x => x.WithGuild(guild).WithChannel(channel));
            if (!guild.CanBotSendMessages(channel))
            {
                Logger.LogInformation("Can't send {GreetByeEventType} message because of missing permissions", type);
                return;
            }

            Logger.LogInformation("Handling event {GreetByeEventType}", type);

            await TriggerEventAsync(setting, guild, channel, user, ct);
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
            GreetByeEventSetting setting,
            IGatewayGuild guild, 
            IMessageGuildChannel targetChannel, 
            IUser user, 
            CancellationToken ct)
        {
            if (setting.Text != default)
                await _sender.SendTextMessageAsync(targetChannel, setting.Text, user, guild, ct);
            else if (setting.Embed != default)
                await _sender.SendEmbedMessageAsync(targetChannel, setting.Embed, user, guild, ct);
            else
                return TriggerEventResult.EventNotSet;

            return TriggerEventResult.Success;
        }
    }
}
