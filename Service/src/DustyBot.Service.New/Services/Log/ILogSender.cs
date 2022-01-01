using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;

namespace DustyBot.Service.Services.Log
{
    internal interface ILogSender
    {
        IEnumerable<LocalMessage> BuildDeletedMessageLogs(IEnumerable<IGatewayUserMessage> messages, IMessageGuildChannel channel);
        Task SendDeletedMessageLogAsync(IMessageGuildChannel targetChannel, IGatewayUserMessage message, IMessageGuildChannel channel, CancellationToken ct);
        Task SendDeletedMessageLogsAsync(IMessageGuildChannel targetChannel, IEnumerable<IGatewayUserMessage> messages, IMessageGuildChannel channel, CancellationToken ct);
    }
}