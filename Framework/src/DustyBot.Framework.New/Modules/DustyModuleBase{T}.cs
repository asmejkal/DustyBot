using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Extensions.Interactivity.Menus.Paged;
using DustyBot.Framework.Commands.Results;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Interactivity;
using DustyBot.Framework.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Modules
{
    public abstract class DustyModuleBase<T> : DiscordModuleBase<T>
        where T : DiscordCommandContext
    {
        private ILogger? _logger;

        protected override ILogger Logger
        {
            get
            {
                if (_logger != null)
                    return _logger;

                var logger = Context.Services.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
                if (Context == null)
                    return _logger = logger;

                return _logger = logger.WithScope(x => x.WithCommandContext(Context));
            }
        }

        protected override ValueTask BeforeExecutedAsync()
        {
            using var scope = Logger.BuildScope(x => x.WithCommandUsageContext(Context));
            if (Context.GuildId != null)
                Logger.LogInformation("Command {MessageContent} with {MessageAttachmentCount} attachments", Context.Message.Content, Context.Message.Attachments.Count);
            else
                Logger.LogInformation("Command {MessageContentRedacted} with {MessageAttachmentCount} attachments", Context.Prefix.ToString() + string.Join(' ', Context.Path), Context.Message.Attachments.Count);

            return new();
        }

        protected virtual DiscordSuccessCommandResult Success()
            => new(Context);

        protected virtual DiscordSuccessResponseCommandResult Success(string content)
            => Success(new LocalMessage().WithContent(content));

        protected virtual DiscordSuccessResponseCommandResult Success(params LocalEmbed[] embeds)
            => Success(new LocalMessage().WithEmbeds(embeds));

        protected virtual DiscordSuccessResponseCommandResult Success(string content, params LocalEmbed[] embeds)
            => Success(new LocalMessage().WithContent(content).WithEmbeds(embeds));

        protected virtual DiscordSuccessResponseCommandResult Success(LocalMessage message)
        {
            message.AllowedMentions ??= LocalAllowedMentions.None;
            if (Context is DiscordGuildCommandContext guildContext && guildContext.Guild.GetBotPermissions(guildContext.Channel).ReadMessageHistory)
                message = message.WithReply(Context.Message.Id, Context.ChannelId, Context.GuildId);

            return new(Context, message);
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
            if (Context is DiscordGuildCommandContext guildContext && guildContext.Guild.GetBotPermissions(guildContext.Channel).ReadMessageHistory)
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
            if (Context is DiscordGuildCommandContext guildContext && guildContext.Guild.GetBotPermissions(guildContext.Channel).ReadMessageHistory)
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
    }
}
