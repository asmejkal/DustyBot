using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;

namespace DustyBot.Service.Services.GreetBye
{
    public interface IGreetByeService
    {
        Task SetEventTextAsync(GreetByeEventType type, Snowflake guildId, ITextChannel channel, string message);

        Task SetEventEmbedAsync(
            GreetByeEventType type,
            Snowflake guildId,
            ITextChannel channel,
            Color? color,
            Uri? image,
            string title,
            string body);

        Task<SetEventEmbedFooterResult> SetEventEmbedFooterAsync(GreetByeEventType type, Snowflake guildId, string? footer);

        Task DisableEventAsync(GreetByeEventType type, Snowflake guildId);

        Task<TriggerEventResult> TriggerEventAsync(GreetByeEventType type, IGatewayGuild guild, IMessageGuildChannel channel, IUser user);
    }

    public enum SetEventEmbedFooterResult
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
