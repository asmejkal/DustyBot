using System;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Database.Mongo.Collections.GreetBye.Models;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Modules;
using DustyBot.Service.Services.GreetBye;
using Qmmands;

namespace DustyBot.Service.Modules
{
    [Name("Greet & Bye")]
    public class GreetByeModule : DustyGuildModuleBase
    {
        private readonly IGreetByeService _service;

        public GreetByeModule(IGreetByeService service)
        {
            _service = service;
        }

        [Group("greet"), Description("Greet members that join your server.")]
        public class GreetSubmodule : GreetByeModule
        {
            public GreetSubmodule(IGreetByeService service) 
                : base(service)
            {
            }

            [Command("text"), Description("Sets a text greeting message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the greeting message.")]
            public async Task<CommandResult> SetGreetTextAsync(
                [Description("a channel that will receive the messages")]
                [RequireBotChannelParameterPermissions(Permission.SendMessages)]
                ITextChannel channel,
                [Description("the greeting message")]
                [Remainder]
                string message)
            {
                await _service.SetEventTextAsync(GreetByeEventType.Greet, Context.GuildId, channel, message, Bot.StoppingToken);
                return Success("Greeting message set.");
            }

            [Command("embed"), Description("Sets an embed greeting message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the greeting message.")]
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
                await _service.SetEventEmbedAsync(GreetByeEventType.Greet, Context.GuildId, channel, title, body, image, color, ct: Bot.StoppingToken);
                return Success("Greeting message set.");
            }

            [VerbCommand("embed", "set", "footer"), Description("Customize a footer for your greeting embed message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders.")]
            [Remark("Use without parameters to hide the footer.")]
            [Example("Member #{membercount}")]
            public async Task<CommandResult> SetGreetEmbedFooterAsync([Remainder] string? text)
            {
                return await _service.UpdateEventEmbedFooterAsync(GreetByeEventType.Greet, Context.GuildId, text, Bot.StoppingToken) switch
                {
                    UpdateEventEmbedFooterResult.EventEmbedNotSet => Failure("You need to set a greeting embed first."),
                    UpdateEventEmbedFooterResult.Success => Success("Greeting embed footer has been set."),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [Command("test"), Description("Sends a sample greeting message in this channel.")]
            public async Task<CommandResult> TestGreetAsync()
            {
                return await _service.TriggerEventAsync(GreetByeEventType.Greet, Context.Guild, Context.Channel, Context.Author, Bot.StoppingToken) switch
                {
                    TriggerEventResult.EventNotSet => Failure("You need to set a greeting first."),
                    TriggerEventResult.Success => Success(),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [Command("disable"), Description("Disables greeting messages.")]
            [RequireAuthorAdministrator]
            public async Task<CommandResult> DisableGreetAsync()
            {
                await _service.DisableEventAsync(GreetByeEventType.Greet, Context.GuildId, Bot.StoppingToken);
                return Success("Greeting has been disabled.");
            }
        }

        [Group("bye"), Description("Say goodbye to members that leave your server.")]
        public class ByeSubmodule : GreetByeModule
        {
            public ByeSubmodule(IGreetByeService service)
                : base(service)
            {
            }

            [Command("text"), Description("Sets a text goodbye message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the goodbye message.")]
            public async Task<CommandResult> SetByeTextAsync(
                [Description("a channel that will receive the messages")]
                [RequireBotChannelParameterPermissions(Permission.SendMessages)]
                ITextChannel channel,
                [Description("the goodbye message")]
                [Remainder]
                string message)
            {
                await _service.SetEventTextAsync(GreetByeEventType.Bye, Context.GuildId, channel, message, Bot.StoppingToken);
                return Success("Goodbye message set.");
            }

            [Command("embed"), Description("Sets an embed goodbye message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the goodbye message.")]
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
                await _service.SetEventEmbedAsync(GreetByeEventType.Bye, Context.GuildId, channel, title, body, image, color, ct: Bot.StoppingToken);
                return Success("Goodbye message set.");
            }

            [VerbCommand("embed", "set", "footer"), Description("Customize a footer for your goodbye embed message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders.")]
            [Remark("Use without parameters to hide the footer.")]
            public async Task<CommandResult> SetByeEmbedFooterAsync([Remainder] string? text)
            {
                return await _service.UpdateEventEmbedFooterAsync(GreetByeEventType.Bye, Context.GuildId, text, Bot.StoppingToken) switch
                {
                    UpdateEventEmbedFooterResult.EventEmbedNotSet => Failure("You need to set a goodbye embed first."),
                    UpdateEventEmbedFooterResult.Success => Success("Goodbye embed footer has been set."),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [Command("test"), Description("Sends a sample goodbye message in this channel.")]
            public async Task<CommandResult> TestByeAsync()
            {
                return await _service.TriggerEventAsync(GreetByeEventType.Bye, Context.Guild, Context.Channel, Context.Author, Bot.StoppingToken) switch
                {
                    TriggerEventResult.EventNotSet => Failure("You need to set a goodbye message first."),
                    TriggerEventResult.Success => Success(),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [Command("disable"), Description("Disables goodbye messages.")]
            [RequireAuthorAdministrator]
            public async Task<CommandResult> DisableByeAsync()
            {
                await _service.DisableEventAsync(GreetByeEventType.Bye, Context.GuildId, Bot.StoppingToken);
                return Success("Goodbye message has been disabled.");
            }
        }
    }
}
