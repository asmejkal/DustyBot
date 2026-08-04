using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Text;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Disqord.Gateway;
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
using Qmmands.Text;

namespace DustyBot.Framework.Modules
{
    public abstract class DustyModuleBase : DiscordTextModuleBase
    {
        private ILogger? _logger;
        private IDisposable? _typingIndicator;

        protected bool HasGuildContext => Context is IDiscordTextGuildCommandContext;

        protected IDiscordTextGuildCommandContext GuildContext => Context switch
        {
            IDiscordTextGuildCommandContext x => x,
            _ => throw new InvalidOperationException("Invalid context")
        };

        protected bool HideInvocation => Context is IDiscordTextGuildCommandContext guildContext
            && guildContext.Command!.HideInvocation()
            && guildContext.Channel is not null
            && guildContext.Bot.GetGuild(guildContext.GuildId)?.GetBotPermissions(guildContext.Channel).HasFlag(Permissions.ManageMessages) == true;

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

        private bool ShouldReply => Context is IDiscordTextGuildCommandContext guildContext
            && !guildContext.Command!.HideInvocation()
            && guildContext.Channel is not null
            && guildContext.Bot.GetGuild(guildContext.GuildId)?.GetBotPermissions(guildContext.Channel).HasFlag(Permissions.ReadMessageHistory) == true;

        public override ValueTask OnBeforeExecuted()
        {
            using var scope = Logger.WithCommandUsageContext(Context).BeginScope();
            if (Context.GuildId != null)
                Logger.LogInformation("Command {MessageContent} with {MessageAttachmentCount} attachments", Context.Message.Content, Context.Message.Attachments.Count);
            else
                Logger.LogInformation("Command {MessageContentRedacted} with {MessageAttachmentCount} attachments", Context.Prefix.ToString() + string.Join(' ', Context.Path ?? Enumerable.Empty<ReadOnlyMemory<char>>()), Context.Message.Attachments.Count);

            if (HideInvocation)
            {
                TaskHelper.FireForget(() => Context.Message.DeleteAsync(cancellationToken: Bot.StoppingToken),
                    ex => Logger.LogError(ex, "Failed to hide command invocation message"));
            }

            if (Context.Command!.IsLongRunning())
                _typingIndicator = Bot.BeginTyping(Context.ChannelId, TimeSpan.FromMinutes(1), cancellationToken: Bot.StoppingToken);

            return default;
        }

        public override ValueTask OnAfterExecuted()
        {
            _typingIndicator?.Dispose();
            return default;
        }

        protected virtual DiscordSuccessCommandResult Success()
            => new(Context);

        protected virtual IDiscordCommandResult Success(string content, TimeSpan deleteAfter = default)
            => Success(new LocalMessage().WithContent(content), deleteAfter);

        protected virtual IDiscordCommandResult Success(params LocalEmbed[] embeds)
            => Success(new LocalMessage().WithEmbeds(embeds));

        protected virtual IDiscordCommandResult Success(string content, params LocalEmbed[] embeds)
            => Success(new LocalMessage().WithContent(content).WithEmbeds(embeds));

        protected virtual IDiscordCommandResult Success(LocalMessage message, TimeSpan deleteAfter = default)
        {
            if (!message.AllowedMentions.HasValue)
                message.AllowedMentions = LocalAllowedMentions.None;
            if (ShouldReply)
                message = message.WithReply(Context.Message.Id, Context.ChannelId, Context.GuildId);

            var result = new DiscordSuccessResponseCommandResult(Context, message);
            return deleteAfter != default ? result.DeleteAfter(deleteAfter) : result;
        }

        protected virtual DiscordFailureResponseCommandResult Failure(string content)
            => Failure(new LocalMessage().WithContent(content));

        protected virtual DiscordFailureResponseCommandResult Failure(params LocalEmbed[] embeds)
            => Failure(new LocalMessage().WithEmbeds(embeds));

        protected virtual DiscordFailureResponseCommandResult Failure(string content, params LocalEmbed[] embeds)
            => Failure(new LocalMessage().WithContent(content).WithEmbeds(embeds));

        protected virtual DiscordFailureResponseCommandResult Failure(LocalMessage message)
        {
            if (!message.AllowedMentions.HasValue)
                message.AllowedMentions = LocalAllowedMentions.None;
            if (ShouldReply)
                message = message.WithReply(Context.Message.Id, Context.ChannelId, Context.GuildId);

            return new(Context, message);
        }

        protected virtual DiscordTextResponseCommandResult Result(string content)
            => Result(new LocalMessage().WithContent(content));

        protected virtual DiscordTextResponseCommandResult Result(params LocalEmbed[] embeds)
            => Result(new LocalMessage().WithEmbeds(embeds));

        protected virtual DiscordTextResponseCommandResult Result(string content, params LocalEmbed[] embeds)
            => Result(new LocalMessage().WithContent(content).WithEmbeds(embeds));

        protected virtual DiscordTextResponseCommandResult Result(LocalMessage message)
        {
            if (!message.AllowedMentions.HasValue)
                message.AllowedMentions = LocalAllowedMentions.None;
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

            var command = method.GetCustomAttribute<TextCommandAttribute>();
            if (command == null)
                throw new ArgumentException("Method is not a command", nameof(methodName));

            var types = new List<Type>();
            for (var current = module; current != null; current = current.DeclaringType)
                types.Add(current);

            var path = new StringBuilder();
            foreach (var type in types.AsEnumerable().Reverse())
            {
                var group = type.GetCustomAttribute<TextGroupAttribute>();
                if (group != null && !string.IsNullOrEmpty(group.Aliases.FirstOrDefault()))
                {
                    path.Append(group.Aliases.First());
                    path.Append(' ');
                }
            }

            if (command is VerbCommandAttribute verbCommand)
                path.Append(string.Join(' ', verbCommand.Verbs) + ' ');

            path.Append(command.Aliases.First());
            return path.ToString();
        }
    }
}
