using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Disqord.Rest;
using DustyBot.Core.Async;
using DustyBot.Framework.Client;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Commands.Results;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Interactivity;
using DustyBot.Framework.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qmmands;

namespace DustyBot.Framework.Modules
{
    public abstract class DustyModuleBase<T> : DiscordModuleBase<T>
        where T : DiscordCommandContext
    {
        private ILogger? _logger;
        private IDisposable _typingIndicator;

        protected bool HideInvocation => Context is DiscordGuildCommandContext guildContext 
            && guildContext.Command.Attributes.OfType<HideInvocationAttribute>().Any()
            && guildContext.Guild.GetBotPermissions(guildContext.Channel).ManageMessages;

        protected override DustyBotSharderBase Bot => (DustyBotSharderBase)base.Bot;

        protected override ILogger Logger
        {
            get
            {
                if (_logger != null)
                    return _logger;

                var logger = Context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
                if (Context == null)
                    return _logger = logger;

                return _logger = logger.WithCommandContext(Context);
            }
        }

        private bool ShouldReply => Context is DiscordGuildCommandContext guildContext
            && !guildContext.Command.Attributes.OfType<HideInvocationAttribute>().Any()
            && guildContext.Guild.GetBotPermissions(guildContext.Channel).ReadMessageHistory;

        protected override ValueTask BeforeExecutedAsync()
        {
            using var scope = Logger.WithCommandUsageContext(Context).BeginScope();
            if (Context.GuildId != null)
                Logger.LogInformation("Command {MessageContent} with {MessageAttachmentCount} attachments", Context.Message.Content, Context.Message.Attachments.Count);
            else
                Logger.LogInformation("Command {MessageContentRedacted} with {MessageAttachmentCount} attachments", Context.Prefix.ToString() + string.Join(' ', Context.Path), Context.Message.Attachments.Count);

            if (HideInvocation)
            {
                TaskHelper.FireForget(() => Context.Message.DeleteAsync(cancellationToken: Bot.StoppingToken),
                    ex => Logger.LogError(ex, "Failed to hide command invocation message"));
            }

            if (Context.Command.IsLongRunning())
                _typingIndicator = Bot.BeginTyping(Context.ChannelId, TimeSpan.FromMinutes(1), cancellationToken: Bot.StoppingToken);

            return default;
        }

        protected override ValueTask AfterExecutedAsync()
        {
            _typingIndicator?.Dispose();
            return default;
        }

        protected virtual DiscordSuccessCommandResult Success()
            => new(Context);

        protected virtual DiscordSuccessResponseCommandResult Success(string content, TimeSpan deleteAfter = default)
            => Success(new LocalMessage().WithContent(content));

        protected virtual DiscordSuccessResponseCommandResult Success(params LocalEmbed[] embeds)
            => Success(new LocalMessage().WithEmbeds(embeds));

        protected virtual DiscordSuccessResponseCommandResult Success(string content, params LocalEmbed[] embeds)
            => Success(new LocalMessage().WithContent(content).WithEmbeds(embeds));

        protected virtual DiscordSuccessResponseCommandResult Success(LocalMessage message, TimeSpan deleteAfter = default)
        {
            message.AllowedMentions ??= LocalAllowedMentions.None;
            if (ShouldReply)
                message = message.WithReply(Context.Message.Id, Context.ChannelId, Context.GuildId);

            return new(Context, message, deleteAfter);
        }

        protected virtual DiscordFailureResponseCommandResult Failure(string content)
            => Failure(new LocalMessage().WithContent(content));

        protected virtual DiscordFailureResponseCommandResult Failure(params LocalEmbed[] embeds)
            => Failure(new LocalMessage().WithEmbeds(embeds));

        protected virtual DiscordFailureResponseCommandResult Failure(string content, params LocalEmbed[] embeds)
            => Failure(new LocalMessage().WithContent(content).WithEmbeds(embeds));

        protected virtual DiscordFailureResponseCommandResult Failure(LocalMessage message)
        {
            message.AllowedMentions ??= LocalAllowedMentions.None;
            if (ShouldReply)
                message = message.WithReply(Context.Message.Id, Context.ChannelId, Context.GuildId);

            return new(Context, message);
        }

        protected virtual DiscordResponseCommandResult Result(string content)
            => Result(new LocalMessage().WithContent(content));

        protected virtual DiscordResponseCommandResult Result(params LocalEmbed[] embeds)
            => Result(new LocalMessage().WithEmbeds(embeds));

        protected virtual DiscordResponseCommandResult Result(string content, params LocalEmbed[] embeds)
            => Result(new LocalMessage().WithContent(content).WithEmbeds(embeds));

        protected virtual DiscordResponseCommandResult Result(LocalMessage message)
        {
            message.AllowedMentions ??= LocalAllowedMentions.None;
            if (ShouldReply)
                message = message.WithReply(Context.Message.Id, Context.ChannelId, Context.GuildId);

            return new(Context, message);
        }

        protected virtual DiscordMenuCommandResult NumberedListing(
            IEnumerable<string> items,
            Action<LocalEmbed>? embedBuilder = null,
            Func<int, string>? prefixFormatter = null,
            int maxItemsPerPage = 15,
            TimeSpan timeout = default)
        {
            return Pages(new NumberedListingPageProvider(items, embedBuilder, prefixFormatter, maxItemsPerPage), timeout);
        }

        protected virtual DiscordMenuCommandResult NumberedListing(
            IEnumerable<string> items,
            string title,
            Func<int, string>? prefixFormatter = null,
            int maxItemsPerPage = 15,
            TimeSpan timeout = default)
        {
            return Pages(new NumberedListingPageProvider(items, x => x.WithTitle(title), prefixFormatter, maxItemsPerPage), timeout);
        }

        protected virtual DiscordMenuCommandResult Listing(
            IEnumerable<LocalEmbedField> items,
            Action<LocalEmbed>? embedBuilder = null,
            int maxItemsPerPage = 10,
            TimeSpan timeout = default)
        {
            return Pages(new FieldListingPageProvider(items, embedBuilder, maxItemsPerPage), timeout);
        }

        protected virtual DiscordMenuCommandResult Listing(
            IEnumerable<LocalEmbedField> items,
            string title,
            int maxItemsPerPage = 10,
            TimeSpan timeout = default)
        {
            return Pages(new FieldListingPageProvider(items, x => x.WithTitle(title), maxItemsPerPage), timeout);
        }

        protected virtual DiscordMenuCommandResult Table(IEnumerable<TableRow> rows, TimeSpan timeout = default)
            => Pages(new TablePageProvider(rows), timeout);

        protected override DiscordMenuCommandResult Pages(PageProvider pageProvider, TimeSpan timeout = default) => 
            View(new AdaptivePagedView(pageProvider), timeout);

        protected string GetReference(string methodName)
        {
            var module = GetType();
            var method = module.GetMethod(methodName);
            if (method == null)
                throw new ArgumentException("Method not found", nameof(methodName));

            var command = method.GetCustomAttribute<CommandAttribute>();
            if (command == null)
                throw new ArgumentException("Method is not a command", nameof(methodName));

            var types = new List<Type>();
            for (var current = module; current != null; current = current.DeclaringType)
                types.Add(current);

            var path = new StringBuilder();
            foreach (var type in types.AsEnumerable().Reverse())
            { 
                var group = type.GetCustomAttribute<GroupAttribute>();
                if (group != null && !string.IsNullOrEmpty(group.Aliases.FirstOrDefault()))
                {
                    path.Append(group.Aliases.First());
                    path.Append(Bot.Commands.Separator);
                }
            }

            if (command is VerbCommandAttribute verbCommand)
                path.Append(string.Join(Bot.Commands.Separator, verbCommand.Verbs) + Bot.Commands.Separator);

            path.Append(command.Aliases.First());
            return path.ToString();
        }
    }
}
