using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using DustyBot.Database.Mongo.Collections.GreetBye.Models;

namespace DustyBot.Service.Services.GreetBye
{
    internal interface IGreetByeSender
    {
        Task SendEmbedMessageAsync(IMessageGuildChannel targetChannel, GreetByeEmbed template, IUser user, IGatewayGuild guild, CancellationToken ct);
        Task SendTextMessageAsync(IMessageGuildChannel targetChannel, string template, IUser user, IGatewayGuild guild, CancellationToken ct);
    }
}