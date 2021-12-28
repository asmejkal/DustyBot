using System;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Core.Formatting;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Modules;
using DustyBot.Service.Services.Log;
using Qmmands;

namespace DustyBot.Service.Modules
{
    [Name("Log"), Description("Log deleted messages and other events.")]
    [Group("log")]
    public class LogModule : DustyGuildModuleBase
    {
        private readonly ILogService _service;

        public LogModule(ILogService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [Command("messages"), Description("Sets a channel for logging of deleted messages.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> EnableMessageLoggingAsync(
            [Description("a channel for the logs")]
            [RequireBotCanSendMessages]
            ITextChannel channel)
        {
            await _service.EnableMessageLoggingAsync(Context.GuildId, channel, Bot.StoppingToken);
            return Success($"Deleted messages will now be logged in the {channel.Mention} channel.");
        }

        [VerbCommand("messages", "disable"), Description("Disables logging of deleted messages.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> DisableMessageLoggingAsync()
        {
            await _service.DisableMessageLoggingAsync(Context.GuildId, Bot.StoppingToken);
            return Success("Logging of deleted messages has been disabled.");
        }

        [VerbCommand("prefix", "filter", "add"), Description("Filters out deleted messages starting with a given prefix (e.g. bot commands).")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> AddPrefixFilterAsync(
            [Description("messages that start with this won't be logged")] 
            [Remainder]
            string prefix)
        {
            await _service.AddPrefixFilterAsync(Context.GuildId, prefix, Bot.StoppingToken);
            return Success($"Deleted messages starting with `{prefix}` will not be logged.");
        }

        [VerbCommand("prefix", "filter", "remove"), Description("Removes a filtered prefix for deleted messages.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> RemovePrefixFilterAsync(
            [Description("messages that start with this will be logged again")]
            [Remainder]
            string prefix)
        {
            return await _service.RemovePrefixFilterAsync(Context.GuildId, prefix, Bot.StoppingToken) switch
            {
                true => Success($"Deleted messages starting with `{prefix}` will be logged again."),
                false => Failure($"There is no filter for prefix `{prefix}`.")
            };
        }

        [VerbCommand("prefix", "filter", "clear"), Description("Clears all filtered prefixes for deleted messages.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> ClearPrefixFiltersAsync()
        {
            await _service.ClearPrefixFiltersAsync(Context.GuildId, Bot.StoppingToken);
            return Success($"Cleared all prefix filters for deleted messages.");
        }

        [VerbCommand("prefix", "filter", "list"), Description("Shows all filtered prefixes for deleted messages.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> ListPrefixFiltersAsync()
        {
            var filters = await _service.GetPrefixFiltersAsync(Context.GuildId, Bot.StoppingToken);
            return NumberedListing(filters.Select(x => Markdown.Escape(x)), "Filtered message prefixes");
        }

        [VerbCommand("channel", "filter", "add"), Description("Excludes one or more channels from logging of deleted messages.")]
        [RequireAuthorAdministrator]
        [Example("#roles #welcome")]
        public async Task<CommandResult> AddChannelFilterAsync(
            [Description("deleted messages from these channels or threads won't be logged anymore")]
            params IMessageGuildChannel[] channels)
        {
            await _service.AddChannelFilterAsync(Context.GuildId, channels, Bot.StoppingToken);
            return Success($"Messages deleted in {channels.Select(x => x.Mention).WordJoin()} will not be logged.");
        }

        [VerbCommand("channel", "filter", "remove"), Description("Removes a channel filter for deleted messages.")]
        [RequireAuthorAdministrator]
        [Example("#roles #welcome")]
        public async Task<CommandResult> RemoveChannelFilterAsync(
            [Description("deleted messages from these channels or threads will be logged again")]
            params IMessageGuildChannel[] channels)
        {
            await _service.RemoveChannelFilterAsync(Context.GuildId, channels, Bot.StoppingToken);
            return Success($"Messages deleted in {channels.Select(x => x.Mention).WordJoin()} will be logged again.");
        }

        [VerbCommand("channel", "filter", "list"), Description("Shows all channels filtered out from logging of deleted messages.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> ListChannelFiltersAsync()
        {
            var filters = await _service.GetChannelFiltersAsync(Context.GuildId, Bot.StoppingToken);
            return NumberedListing(filters.Select(x => Mention.Channel(x)), "Filtered channels");
        }
    }
}
