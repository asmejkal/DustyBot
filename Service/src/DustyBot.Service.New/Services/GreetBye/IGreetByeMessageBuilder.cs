using Disqord;
using Disqord.Gateway;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Service.Services.GreetBye
{
    public interface IGreetByeMessageBuilder
    {
        string BuildText(string template, IUser member, IGatewayGuild guild);
        LocalEmbed BuildEmbed(GreetByeEmbed template, IUser member, IGatewayGuild guild);
    }
}