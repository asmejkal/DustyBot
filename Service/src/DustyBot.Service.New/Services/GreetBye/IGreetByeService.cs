using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using DustyBot.Database.Mongo.Collections.GreetBye.Models;

namespace DustyBot.Service.Services.GreetBye
{
    public interface IGreetByeService
    {
        Task SetEventTextAsync(GreetByeEventType type, Snowflake guildId, ITextChannel channel, string message, CancellationToken ct);

        Task SetEventEmbedAsync(
            GreetByeEventType type,
            Snowflake guildId,
            ITextChannel channel,
            string title,
            string body,
            Uri? image = null,
            Color? color = null,
            string? footer = null, 
            CancellationToken ct = default);

        Task<UpdateEventEmbedFooterResult> UpdateEventEmbedFooterAsync(GreetByeEventType type, Snowflake guildId, string? footer, CancellationToken ct);

        Task DisableEventAsync(GreetByeEventType type, Snowflake guildId, CancellationToken ct);

        Task<TriggerEventResult> TriggerEventAsync(GreetByeEventType type, IGatewayGuild guild, IMessageGuildChannel channel, IUser user, CancellationToken ct);
    }

    public enum UpdateEventEmbedFooterResult
    {
        Success,
        EventEmbedNotSet
    }

    public enum TriggerEventResult
    {
        Success,
        EventNotSet
    }
}
