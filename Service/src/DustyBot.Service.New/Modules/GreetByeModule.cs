using System;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Framework.Attributes;
using DustyBot.Framework.Modules;
using DustyBot.Service.Services.GreetBye;
using Qmmands;

namespace DustyBot.Service.Modules
{
    [Name("Greet & Bye"), Description("Greet & bye messages.")]
    public class GreetByeModule : DustyGuildModuleBase
    {
        private readonly IGreetByeService _service;

        public GreetByeModule(IGreetByeService service)
        {
            _service = service;
        }

        [VerbCommand("greet", "text"), Description("Sets a text greeting message.")]
        [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the greeting message.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> SetGreetTextAsync(
            [Description("a channel that will receive the messages")]
            [RequireBotChannelParameterPermissions(Permission.SendMessages)]
            ITextChannel channel,
            [Description("the greeting message")]
            [Remainder]
            string message)
        {
            await _service.SetEventTextAsync(GreetByeEventType.Greet, Context.GuildId, channel, message);
            return Success("Greeting message set.");
        }

        [VerbCommand("greet", "embed"), Description("Sets an embed greeting message.")]
        [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the greeting message.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> SetGreetEmbedAsync(
            [Description("a channel that will receive the messages")]
            [RequireBotChannelParameterPermissions(Permission.SendEmbeds)]
            ITextChannel channel,
            [Description("hex code of a color (e.g. `#09A5BC`)")]
            Color? color,
            [Description("link to an image")]
            Uri? image,
            [Description("title of the message")]
            string title,
            [Description("body of the greeting message")]
            [Remainder]
            string body)
        {
            await _service.SetEventEmbedAsync(GreetByeEventType.Greet, Context.GuildId, channel, color, image, title, body);
            return Success("Greeting message set.");
        }

        [VerbCommand("greet", "embed", "set", "footer"), Description("Customize a footer for your greeting embed message.")]
        [RequireAuthorAdministrator]
        [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders.")]
        [Remark("Use without parameters to hide the footer.")]
        [Example("Member #{membercount}")]
        public async Task<CommandResult> SetGreetEmbedFooterAsync([Remainder] string? text)
        {
            return await _service.SetEventEmbedFooterAsync(GreetByeEventType.Greet, Context.GuildId, text) switch
            {
                SetEventEmbedFooterResult.EventEmbedNotSet => Failure("You need to set a greeting embed first."),
                SetEventEmbedFooterResult.Success => Success("Greeting embed footer has been set."),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [VerbCommand("greet", "disable"), Description("Disables greeting messages.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> DisableGreetAsync()
        {
            await _service.DisableEventAsync(GreetByeEventType.Greet, Context.GuildId);
            return Success("Greeting has been disabled.");
        }

        [VerbCommand("greet", "test"), Description("Sends a sample greeting message in this channel.")]
        public async Task<CommandResult> TestGreetAsync()
        {
            return await _service.TriggerEventAsync(GreetByeEventType.Greet, Context.Guild, Context.Channel, Context.Author) switch
            {
                TriggerEventResult.EventNotSet => Failure("You need to set a greeting first."),
                TriggerEventResult.Success => Success(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [VerbCommand("bye", "text"), Description("Sets a text goodbye message.")]
        [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the goodbye message.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> SetByeTextAsync(
            [Description("a channel that will receive the messages")]
            [RequireBotChannelParameterPermissions(Permission.SendMessages)]
            ITextChannel channel,
            [Description("the goodbye message")]
            [Remainder]
            string message)
        {
            await _service.SetEventTextAsync(GreetByeEventType.Bye, Context.GuildId, channel, message);
            return Success("Goodbye message set.");
        }

        [VerbCommand("bye", "embed"), Description("Sets an embed goodbye message.")]
        [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the goodbye message.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> SetByeEmbedAsync(
            [Description("a channel that will receive the messages")]
            [RequireBotChannelParameterPermissions(Permission.SendEmbeds)]
            ITextChannel channel,
            [Description("hex code of a color (e.g. `#09A5BC`)")]
            Color? color,
            [Description("link to an image")]
            Uri? image,
            [Description("title of the message")]
            string title,
            [Description("body of the message")]
            [Remainder]
            string body)
        {
            await _service.SetEventEmbedAsync(GreetByeEventType.Bye, Context.GuildId, channel, color, image, title, body);
            return Success("Goodbye message set.");
        }

        [VerbCommand("bye", "embed", "set", "footer"), Description("Customize a footer for your goodbye embed message.")]
        [RequireAuthorAdministrator]
        [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders.")]
        [Remark("Use without parameters to hide the footer.")]
        public async Task<CommandResult> SetByeEmbedFooterAsync([Remainder] string? text)
        {
            return await _service.SetEventEmbedFooterAsync(GreetByeEventType.Bye, Context.GuildId, text) switch
            {
                SetEventEmbedFooterResult.EventEmbedNotSet => Failure("You need to set a goodbye embed first."),
                SetEventEmbedFooterResult.Success => Success("Goodbye embed footer has been set."),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [VerbCommand("bye", "disable"), Description("Disables goodbye messages.")]
        [RequireAuthorAdministrator]
        public async Task<CommandResult> DisableByeAsync()
        {
            await _service.DisableEventAsync(GreetByeEventType.Bye, Context.GuildId);
            return Success("Goodbye message has been disabled.");
        }

        [VerbCommand("bye", "test"), Description("Sends a sample goodbye message in this channel.")]
        public async Task<CommandResult> TestByeAsync()
        {
            return await _service.TriggerEventAsync(GreetByeEventType.Bye, Context.Guild, Context.Channel, Context.Author) switch
            {
                TriggerEventResult.EventNotSet => Failure("You need to set a goodbye message first."),
                TriggerEventResult.Success => Success(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
