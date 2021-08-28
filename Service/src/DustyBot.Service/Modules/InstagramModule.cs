using System.Threading.Tasks;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Modules.Attributes;

namespace DustyBot.Service.Modules
{
    [Module("Instagram", "Show previews for your Instagram links.", true)]
    internal sealed class InstagramModule
    {
        public InstagramModule()
        {
        }

        [Command("ig", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("instagram", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply("Unfortunately, this module had to be disabled due to a block from Instagram.");
        }

        [Command("ig", "Shows a preview of one or more Instagram posts.")]
        [Alias("instagram")]
        public async Task Instagram(ICommand command)
        {
            await command.Reply("Unfortunately, this module had to be disabled due to a block from Instagram.");
        }
    }
}
