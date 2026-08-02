using System;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using DustyBot.Database.Mongo.Collections.GreetBye.Models;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Modules;
using DustyBot.Service.Services.GreetBye;
using Qmmands;
using Qmmands.Text;
using Disqord.Bot.Commands;

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

        [TextGroup("greet"), Description("Greet members that join your server.")]
        public class GreetSubmodule : GreetByeModule
        {
            public GreetSubmodule(IGreetByeService service) 
                : base(service)
            {
            }

            [TextCommand("text"), Description("Sets a text greeting message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the greeting message.")]
            public async Task<IDiscordCommandResult> SetGreetTextAsync(
                [Description("a channel that will receive the messages")]
                [RequireBotCanSendMessages]
                IMessageGuildChannel channel,
                [Description("the greeting message")]
                [Remainder]
                string message)
            {
                await _service.SetEventTextAsync(GreetByeEventType.Greet, GuildContext.GuildId, channel, message, Bot.StoppingToken);
                return Success("Greeting message set.");
            }

            [TextCommand("embed"), Description("Sets an embed greeting message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the message.")]
            [Example("#general #09A5BC https://imgur.com/picture.jpg \nHello, {mention}.\nDon't forget to check out the #rules!")]
            public async Task<IDiscordCommandResult> SetEmbedAsync(
                [Description("a channel or thread that will receive the messages")]
                [RequireBotCanSendEmbeds]
                IMessageGuildChannel channel,
                [Description("hex code of a color (e.g. `#09A5BC`)")]
                Color? color,
                [Description("link to an image (alternatively you can add it as an attachment)")]
                Uri? image,
                [Description("body of the greeting message")]
                [Remainder]
                string body)
            {
                if (Context.Message.Attachments.Any())
                    image ??= new(Context.Message.Attachments.First().Url);

                var embed = new GreetByeEmbed(body, image: image, color: color?.RawValue);
                await _service.SetEventEmbedAsync(GreetByeEventType.Greet, GuildContext.GuildId, channel, embed, Bot.StoppingToken);
                return Success("Greeting message set.");
            }

            [VerbCommand("embed", "set", "title"), Description("Customize a title for your greeting embed message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders.")]
            [Remark("Use without parameters to hide the title.")]
            [Example("Welcome to {server}, {name}!")]
            public async Task<IDiscordCommandResult> SetEmbedTitleAsync([Remainder] string? text)
            {
                return await _service.UpdateEventEmbedAsync(GreetByeEventType.Greet, GuildContext.GuildId, new GreetByeEmbedUpdate() { Title = text }, Bot.StoppingToken) switch
                {
                    UpdateEventEmbedResult.EventEmbedNotSet => Failure($"You first need to set a greeting embed with {GetReference(nameof(SetEmbedAsync))}."),
                    UpdateEventEmbedResult.Success => Success("Greeting embed title has been set."),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [VerbCommand("embed", "set", "footer"), Description("Customize a footer for your greeting embed message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders.")]
            [Remark("Use without parameters to hide the footer.")]
            [Example("Member #{membercount}")]
            public async Task<IDiscordCommandResult> SetEmbedFooterAsync([Remainder] string? text)
            {
                return await _service.UpdateEventEmbedAsync(GreetByeEventType.Greet, GuildContext.GuildId, new GreetByeEmbedUpdate() { Footer = text }, Bot.StoppingToken) switch
                {
                    UpdateEventEmbedResult.EventEmbedNotSet => Failure("You need to set a greeting embed first."),
                    UpdateEventEmbedResult.Success => Success("Greeting embed footer has been set."),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [VerbCommand("embed", "set", "text"), Description("Customize plain text for your greeting embed message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders.")]
            [Remark("Use without parameters to hide the text.")]
            [Example("Welcome, {mention}!")]
            public async Task<IDiscordCommandResult> SetEmbedTextAsync(
                [Description("text that will show above the embed")]
                [Remainder] string? text)
            {
                return await _service.UpdateEventEmbedAsync(GreetByeEventType.Greet, GuildContext.GuildId, new GreetByeEmbedUpdate() { Text = text }, Bot.StoppingToken) switch
                {
                    UpdateEventEmbedResult.EventEmbedNotSet => Failure("You need to set a greeting embed first."),
                    UpdateEventEmbedResult.Success => Success("Greeting embed text has been set."),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [TextCommand("test"), Description("Sends a sample greeting message in this channel.")]
            public async Task<IDiscordCommandResult> TestGreetAsync()
            {
                return await _service.TriggerEventAsync(GreetByeEventType.Greet, Bot.GetGuild(GuildContext.GuildId)!, GuildContext.Channel, Context.Author, Bot.StoppingToken) switch
                {
                    TriggerEventResult.EventNotSet => Failure("You need to set a greeting first."),
                    TriggerEventResult.Success => Success(),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [TextCommand("disable"), Description("Disables greeting messages.")]
            [RequireAuthorAdministrator]
            public async Task<IDiscordCommandResult> DisableGreetAsync()
            {
                await _service.DisableEventAsync(GreetByeEventType.Greet, GuildContext.GuildId, Bot.StoppingToken);
                return Success("Greeting has been disabled.");
            }
        }

        [TextGroup("bye"), Description("Say goodbye to members that leave your server.")]
        public class ByeSubmodule : GreetByeModule
        {
            public ByeSubmodule(IGreetByeService service)
                : base(service)
            {
            }

            [TextCommand("text"), Description("Sets a text goodbye message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the goodbye message.")]
            public async Task<IDiscordCommandResult> SetByeTextAsync(
                [Description("a channel that will receive the messages")]
                [RequireBotCanSendMessages]
                IMessageGuildChannel channel,
                [Description("the goodbye message")]
                [Remainder]
                string message)
            {
                await _service.SetEventTextAsync(GreetByeEventType.Bye, GuildContext.GuildId, channel, message, Bot.StoppingToken);
                return Success("Goodbye message set.");
            }

            [TextCommand("embed"), Description("Sets an embed greeting message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders in the message.")]
            [Example("#general #09A5BC https://imgur.com/picture.jpg \nHello, {mention}.\nDon't forget to check out the #rules!")]
            public async Task<IDiscordCommandResult> SetEmbedAsync(
                [Description("a channel or thread that will receive the messages")]
                [RequireBotCanSendEmbeds]
                IMessageGuildChannel channel,
                [Description("hex code of a color (e.g. `#09A5BC`)")]
                Color? color,
                [Description("link to an image (alternatively you can add it as an attachment)")]
                Uri? image,
                [Description("body of the goodbye message")]
                [Remainder]
                string body)
            {
                if (Context.Message.Attachments.Any())
                    image ??= new(Context.Message.Attachments.First().Url);

                var embed = new GreetByeEmbed(body, image: image, color: color?.RawValue);
                await _service.SetEventEmbedAsync(GreetByeEventType.Bye, GuildContext.GuildId, channel, embed, Bot.StoppingToken);
                return Success("Goodbye message set.");
            }

            [VerbCommand("embed", "set", "title"), Description("Customize a title for your goodbye embed message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders.")]
            [Remark("Use without parameters to hide the title.")]
            [Example("Welcome to {server}, {name}!")]
            public async Task<IDiscordCommandResult> SetEmbedTitleAsync([Remainder] string? text)
            {
                return await _service.UpdateEventEmbedAsync(GreetByeEventType.Bye, GuildContext.GuildId, new GreetByeEmbedUpdate() { Title = text }, Bot.StoppingToken) switch
                {
                    UpdateEventEmbedResult.EventEmbedNotSet => Failure($"You first need to set a goodbye embed with {GetReference(nameof(SetEmbedAsync))}."),
                    UpdateEventEmbedResult.Success => Success("Goodbye embed title has been set."),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [VerbCommand("embed", "set", "footer"), Description("Customize a footer for your goodbye embed message.")]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders.")]
            [Remark("Use without parameters to hide the footer.")]
            [Example("Member #{membercount}")]
            public async Task<IDiscordCommandResult> SetEmbedFooterAsync([Remainder] string? text)
            {
                return await _service.UpdateEventEmbedAsync(GreetByeEventType.Bye, GuildContext.GuildId, new GreetByeEmbedUpdate() { Footer = text }, Bot.StoppingToken) switch
                {
                    UpdateEventEmbedResult.EventEmbedNotSet => Failure("You need to set a goodbye embed first."),
                    UpdateEventEmbedResult.Success => Success("Goodbye embed footer has been set."),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [VerbCommand("embed", "set", "text"), Description("Customize plain text for your goodbye embed message.")]
            [RequireAuthorAdministrator]
            [Remark($"You can use {GreetByeMessagePlaceholders.PlaceholderList} placeholders.")]
            [Remark("Use without parameters to hide the text.")]
            [Example("Welcome, {mention}!")]
            public async Task<IDiscordCommandResult> SetEmbedTextAsync(
                [Description("text that will show above the embed")]
                [Remainder] string? text)
            {
                return await _service.UpdateEventEmbedAsync(GreetByeEventType.Bye, GuildContext.GuildId, new GreetByeEmbedUpdate() { Text = text }, Bot.StoppingToken) switch
                {
                    UpdateEventEmbedResult.EventEmbedNotSet => Failure("You need to set a goodbye embed first."),
                    UpdateEventEmbedResult.Success => Success("Goodbye embed text has been set."),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [TextCommand("test"), Description("Sends a sample goodbye message in this channel.")]
            public async Task<IDiscordCommandResult> TestByeAsync()
            {
                return await _service.TriggerEventAsync(GreetByeEventType.Bye, Bot.GetGuild(GuildContext.GuildId)!, GuildContext.Channel, Context.Author, Bot.StoppingToken) switch
                {
                    TriggerEventResult.EventNotSet => Failure("You need to set a goodbye message first."),
                    TriggerEventResult.Success => Success(),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            [TextCommand("disable"), Description("Disables goodbye messages.")]
            [RequireAuthorAdministrator]
            public async Task<IDiscordCommandResult> DisableByeAsync()
            {
                await _service.DisableEventAsync(GreetByeEventType.Bye, GuildContext.GuildId, Bot.StoppingToken);
                return Success("Goodbye message has been disabled.");
            }
        }
    }
}
