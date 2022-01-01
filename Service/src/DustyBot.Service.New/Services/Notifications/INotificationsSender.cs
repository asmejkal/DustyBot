using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using DustyBot.Database.Mongo.Collections.Notifications.Models;

namespace DustyBot.Service.Services.Notifications
{
    internal interface INotificationsSender
    {
        Task SendNotificationAsync(
            IMember recipient,
            IGatewayUserMessage message,
            Notification notification,
            IGuild guild,
            IMessageGuildChannel sourceChannel,
            CancellationToken ct);

        Task SendQuotaReachedWarningAsync(IMember recipient, IGuild guild, TimeSpan expiresIn, CancellationToken ct);
    }
}