using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Core.Formatting;
using DustyBot.Database.Mongo.Collections.Notifications.Models;
using DustyBot.Service.Communication;

namespace DustyBot.Service.Services.Notifications
{
    internal class NotificationsSender : INotificationsSender
    {
        public Task SendNotificationAsync(
            IMember recipient,
            IGatewayUserMessage message,
            Notification notification,
            IGuild guild,
            IMessageGuildChannel sourceChannel,
            CancellationToken ct)
        {
            var footer = $"\n\n{Markdown.Timestamp(message.CreatedAt(), Markdown.TimestampFormat.RelativeTime)} | {Markdown.Link("Show", message.GetJumpUrl())} | {sourceChannel.Mention}";

            var embed = new LocalEmbed()
                .WithAuthor(message.Author.Name, message.Author.GetAvatarUrl(), message.GetJumpUrl())
                .WithDescription(message.Content.Truncate(LocalEmbed.MaxDescriptionLength - footer.Length) + footer);

            var result = new LocalMessage()
                .WithContent($"{DefaultEmoji.Bell} `{message.Author.Name}` mentioned `{notification.Keyword}` on `{guild.Name}`:")
                .WithEmbeds(embed);

            return recipient.SendMessageAsync(result, cancellationToken: ct);
        }

        public Task SendQuotaReachedWarningAsync(IMember recipient, IGuild guild, TimeSpan expiresIn, CancellationToken ct)
        {
            var result = new LocalMessage()
                .WithContent($"You've reached your daily notification limit on server `{guild.Name}`. Your quota will reset in `{expiresIn.SimpleFormat()}`.\n"
                    + "We're sorry, but this is a necessary safeguard to prevent abuse.");

            return recipient.SendMessageAsync(result, cancellationToken: ct);
        }
    }
}
