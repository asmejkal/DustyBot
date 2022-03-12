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
        Task SetEventTextAsync(GreetByeEventType type, Snowflake guildId, IMessageGuildChannel channel, string message, CancellationToken ct);

        Task SetEventEmbedAsync(
            GreetByeEventType type,
            Snowflake guildId,
            IMessageGuildChannel channel,
            GreetByeEmbed embed,
            CancellationToken ct);

        Task<UpdateEventEmbedResult> UpdateEventEmbedAsync(
            GreetByeEventType type,
            Snowflake guildId,
            GreetByeEmbedUpdate update,
            CancellationToken ct);

        Task DisableEventAsync(GreetByeEventType type, Snowflake guildId, CancellationToken ct);

        Task<TriggerEventResult> TriggerEventAsync(GreetByeEventType type, IGatewayGuild guild, IMessageGuildChannel channel, IUser user, CancellationToken ct);
    }

    public enum UpdateEventEmbedResult
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
