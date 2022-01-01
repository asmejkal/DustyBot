using Disqord.Bot.Hosting;

namespace DustyBot.Framework.Services
{
    public abstract class DustyBotService : DiscordBotService
    {
        public new DustyBotSharderBase Bot => (DustyBotSharderBase)base.Bot;
    }
}
