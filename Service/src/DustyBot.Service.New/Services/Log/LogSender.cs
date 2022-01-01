using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Core.Formatting;

namespace DustyBot.Service.Services.Log
{
    internal class LogSender : ILogSender
    {
        public Task SendDeletedMessageLogAsync(IMessageGuildChannel targetChannel, IGatewayUserMessage message, IMessageGuildChannel channel, CancellationToken ct)
        {
            var preface = $"**Message by {message.Author.Mention} in {channel.Mention} was deleted:**\n";
            var embed = new LocalEmbed()
                .WithDescription(preface + message.Content.Truncate(LocalEmbed.MaxDescriptionLength - preface.Length))
                .WithTimestamp(message.CreatedAt());

            if (message.Attachments.Any())
            {
                var builder = new StringBuilder();
                foreach (var attachment in message.Attachments)
                {
                    if (!builder.TryAppendLineLimited(attachment.Url, LocalEmbedField.MaxFieldValueLength))
                        break;
                }

                embed.AddField("Attachments", builder.ToString());
            }

            return targetChannel.SendMessageAsync(new LocalMessage().WithEmbeds(embed), cancellationToken: ct);
        }

        public async Task SendDeletedMessageLogsAsync(
            IMessageGuildChannel targetChannel,
            IEnumerable<IGatewayUserMessage> messages,
            IMessageGuildChannel channel,
            CancellationToken ct)
        {
            foreach (var log in BuildDeletedMessageLogs(messages, channel))
                await targetChannel.SendMessageAsync(log, cancellationToken: ct);
        }

        public IEnumerable<LocalMessage> BuildDeletedMessageLogs(IEnumerable<IGatewayUserMessage> messages, IMessageGuildChannel channel)
        {
            var logs = new List<string>();
            foreach (var message in messages)
            {
                var preface = $"**Message by {message.Author.Mention} in {channel.Mention} was deleted:**\n";
                var footer = Markdown.Timestamp(message.CreatedAt(), Markdown.TimestampFormat.ShortDateTime);

                var attachments = new StringBuilder();
                if (message.Attachments.Any())
                {
                    foreach (var attachment in message.Attachments)
                    {
                        if (!attachments.TryAppendLineLimited(attachment.Url, LocalEmbed.MaxDescriptionLength / 2))
                            break;
                    }

                    attachments.Append('\n');
                }

                var builder = new StringBuilder(preface);
                if (!string.IsNullOrWhiteSpace(message.Content))
                {
                    builder.Append(message.Content.Truncate(LocalEmbed.MaxDescriptionLength - preface.Length - footer.Length - attachments.Length - 1));
                    builder.Append('\n');
                }

                builder.Append(attachments);
                builder.Append(footer);
            }

            var embed = new LocalEmbed();
            var delimiter = "\n\n";
            foreach (var log in logs)
            {
                var totalLength = log.Length + delimiter.Length;

                if ((embed.Description?.Length ?? 0) + totalLength > LocalEmbed.MaxDescriptionLength)
                {
                    yield return new LocalMessage().WithEmbeds(embed);
                    embed = new LocalEmbed();
                }
                else
                {
                    embed.Description += log + delimiter;
                }
            }

            if (!string.IsNullOrEmpty(embed.Description))
                yield return new LocalMessage().WithEmbeds(embed);
        }
    }
}
