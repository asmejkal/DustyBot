using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Modules;
using DustyBot.Service.Services.Notifications;
using Qmmands;

namespace DustyBot.Service.Modules
{
    [Name("Notifications"), Description("Get notified when anyone mentions a specific word.")]
    [Group("notifications", "notification", "notif", "noti")]
    public class NotificationsModule : DustyModuleBase
    {
        private readonly INotificationsService _service;

        public NotificationsModule(INotificationsService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [Command("add"), Description("Adds a word that will send you a notification when it is mentioned in this server."), RequireGuild]
        [HideInvocation]
        public async Task<CommandResult> AddKeywordAsync(
            [Description("when this word is mentioned in this server you will receive a notification")]
            [Remainder]
            string keyword)
        {
            return await _service.AddKeywordAsync(GuildContext.GuildId, Context.Author.Id, keyword, Bot.StoppingToken) switch
            {
                AddKeywordResult.KeywordTooShort => Failure("This keyword is too short."),
                AddKeywordResult.KeywordTooLong => Failure("This keyword is too long."),
                AddKeywordResult.TooManyKeywords => Failure($"You have too many keywords on this server. You can remove old keywords with `{GetReference(nameof(RemoveKeywordAsync))}`."),
                AddKeywordResult.DuplicateKeyword => Failure("You are already being notified for this keyword."),
                AddKeywordResult.Success => Success("You will now be notified when this word is mentioned. Please make sure your privacy settings allow the bot to DM you."),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [Command("remove"), Description("Removes a notified word."), RequireGuild]
        [HideInvocation]
        public async Task<CommandResult> RemoveKeywordAsync(
            [Description("a word you don't want to be notified for anymore")]
            [Remainder]
            string keyword)
        {
            return await _service.RemoveKeywordAsync(GuildContext.GuildId, Context.Author.Id, keyword, Bot.StoppingToken) switch
            {
                RemoveKeywordResult.NotFound => Failure($"You don't have this keyword set as a notification. You can see all your keywords with `{GetReference(nameof(AddKeywordAsync))}`."),
                RemoveKeywordResult.Success => Success("You will no longer be notified when this word is mentioned."),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [Command("clear"), Description("Removes all notified words on this server."), RequireGuild]
        public async Task<CommandResult> ClearKeywordsAsync()
        {
            await _service.ClearKeywordsAsync(GuildContext.GuildId, Context.Author.Id, Bot.StoppingToken);
            return Success("You will no longer be notified on this server.");
        }

        [Command("pause"), Description("Disables all notifications on this server until you turn them back on."), RequireGuild]
        public async Task<CommandResult> PauseNotificationsAsync()
        {
            await _service.PauseNotificationsAsync(GuildContext.GuildId, Context.Author.Id, Bot.StoppingToken);
            return Success($"You won't get notifications on this server until you use `{GetReference(nameof(ResumeNotificationsAsync))}`.");
        }

        [Command("resume", "unpause"), Description("Turns paused notifications back on."), RequireGuild]
        public async Task<CommandResult> ResumeNotificationsAsync()
        {
            await _service.ResumeNotificationsAsync(GuildContext.GuildId, Context.Author.Id, Bot.StoppingToken);
            return Success("You will get notifications on this server again.");
        }

        [Command("block"), Description("Blocks a person from triggering your notifications.")]
        [HideInvocation]
        [Remark("You will not get notifications for any messages from a blocked person.")]
        [Remark("This command can be used in a DM. See [this guide](https://support.discord.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID-) if you don't know how to find the user ID.")]
        public async Task<CommandResult> BlockUserAsync(
            [Description("a user ID or a mention")]
            IUser user)
        {
            await _service.BlockUserAsync(Context.Author.Id, user.Id, Bot.StoppingToken);
            return Success("You will no longer receive notifications for any messages from this person.", TimeSpan.FromSeconds(3));
        }

        [Command("unblock"), Description("Unblocks a person.")]
        [HideInvocation]
        [Remark("This command can be used in a DM. You will receive notifications for messages from this person again.")]
        public async Task<CommandResult> UnblockUserAsync(
            [Description("a user ID or a mention")]
            IUser user)
        {
            await _service.UnblockUserAsync(Context.Author.Id, user.Id, Bot.StoppingToken);
            return Success("You will receive notifications for messages from this person again.", TimeSpan.FromSeconds(3));
        }

        [VerbCommand("ignore", "channel"), Description("Ignore messages in this channel or thread for notifications. Use again to un-ignore."), RequireGuild]
        public async Task<CommandResult> ToggleIgnoredChannelAsync()
        {
            return await _service.ToggleIgnoredChannelAsync(GuildContext.GuildId, Context.Author.Id, Context.ChannelId, Bot.StoppingToken) switch
            {
                true => Success("You will no longer receive notifications from this channel."),
                false => Success("You will now receive notifications from this channel.")
            };
        }

        [VerbCommand("ignore", "active", "channel"), Description("Skip notifications from channels that you're currently active in.")]
        [Remark("All notifications will be delayed by a small amount. If a keyword is mentioned and you start typing a response before the notification arrives, you won't be notified.")]
        [Remark("Use this command again to disable.")]
        public async Task<CommandResult> ToggleActivityDetectionAsync()
        {
            return await _service.ToggleActivityDetectionAsync(Context.Author.Id, Bot.StoppingToken) switch
            {
                true => Success("You won't be notified for messages in channels you're currently being active in. This causes a small delay for all notifications."),
                false => Success("You will now be notified for every message instantly.")
            };
        }

        [Command("list"), Description("Lists all your notified words on this server."), RequireGuild]
        [Remark("Sends a direct message.")]
        public async Task<CommandResult> ListKeywordsAsync()
        {
            var keywords = await _service.GetKeywordsAsync(GuildContext.GuildId, Context.Author.Id, Bot.StoppingToken);
            var result = new StringBuilder();
            foreach (var keyword in keywords.OrderByDescending(x => x.TriggerCount))
                result.AppendLine($"`{keyword.Keyword}` – notified `{keyword.TriggerCount}` times");

            if (result.Length <= 0)
                return Result($"You don't have any notified words on this server. Use `{GetReference(nameof(AddKeywordAsync))}` to add some.");

            var embed = new LocalEmbed()
                .WithAuthor($"Your notifications on {GuildContext.Guild.Name}", GuildContext.Guild.GetIconUrl())
                .WithDescription(result.ToString());

            try
            {
                await Context.Author.SendMessageAsync(new LocalMessage().WithEmbeds(embed), cancellationToken: Bot.StoppingToken);
            }
            catch (RestApiException ex) when (ex.IsError(RestApiErrorCode.CannotSendMessagesToThisUser))
            {
                return Failure("Failed to send a direct message. Please check that your privacy settings allow the bot to DM you.");
            }

            return Success("Please check your direct messages.");
        }
    }
}
