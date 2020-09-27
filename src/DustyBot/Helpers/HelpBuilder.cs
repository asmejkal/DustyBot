using Discord;
using DustyBot.Database.Services;
using DustyBot.Definitions;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    static class HelpBuilder
    {
        public static string GetModuleWebLink(IModule module) => 
            DiscordHelpers.SanitiseMarkdownUri(WebConstants.ReferenceUrl + "#" + WebConstants.GetModuleWebAnchor(module.Name));

        public static async Task<Embed> GetModuleHelpEmbed(IModule module, ISettingsService settings)
        {
            var config = await settings.ReadGlobal<BotConfig>();
            return new EmbedBuilder()
                .WithDescription($"● For a list of commands in this module, please see the [website]({GetModuleWebLink(module)}).\n● Type `{config.CommandPrefix}help command name` to see quick help for a specific command.\n\nIf you need further assistance or have any questions, please join the [support server]({WebConstants.SupportServerInvite}).")
                .WithTitle($"❔ {module.Name} help").Build();
        }

        public static async Task<EmbedBuilder> GetHelpEmbed(ISettingsService settings)
        {
            var config = await settings.ReadGlobal<BotConfig>();
            return new EmbedBuilder()
                .WithDescription($"● For a full list of commands see the [website]({WebConstants.ReferenceUrl}).\n● Type `{config.CommandPrefix}help command name` to see quick help for a specific command.\n\nIf you need further assistance or have any questions, please join the [support server]({WebConstants.SupportServerInvite}).")
                .WithTitle("❔ Help");
        }
    }
}
