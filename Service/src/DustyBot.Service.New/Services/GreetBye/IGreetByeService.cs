using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using DustyBot.Database.Mongo.Collections.GreetBye.Models;

namespace DustyBot.Service.Services.GreetBye
{
    public interface IGreetByeService
    {
        Task SetEventTextAsync(GreetByeEventType type, Snowflake guildId, ITextChannel channel, string message);

        Task SetEventEmbedAsync(
            GreetByeEventType type,
            Snowflake guildId,
            ITextChannel channel,
            string title,
            string body,
            Uri? image = null,
            Color? color = null,
            string? footer = null);

        Task<UpdateEventEmbedFooterResult> UpdateEventEmbedFooterAsync(GreetByeEventType type, Snowflake guildId, string? footer);

        Task DisableEventAsync(GreetByeEventType type, Snowflake guildId);

        Task<TriggerEventResult> TriggerEventAsync(GreetByeEventType type, IGatewayGuild guild, IMessageGuildChannel channel, IUser user);
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
