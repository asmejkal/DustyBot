using Discord;
using DustyBot.Definitions;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Utility;

namespace DustyBot.Helpers
{
    static class HelpBuilder
    {
        public static string GetModuleWebLink(IModule module) => 
            DiscordHelpers.SanitiseMarkdownUri(WebConstants.ReferenceUrl + "#" + WebConstants.GetModuleWebAnchor(module.Name));

        public static Embed GetModuleHelpEmbed(IModule module, string commandPrefix)
        {
            return new EmbedBuilder()
                .WithDescription($"● For a list of commands in this module, please see the [website]({GetModuleWebLink(module)}).\n● Type `{commandPrefix}help command name` to see quick help for a specific command.\n\nIf you need further assistance or have any questions, please join the [support server]({WebConstants.SupportServerInvite}).")
                .WithTitle($"❔ {module.Name} help").Build();
        }

        public static EmbedBuilder GetHelpEmbed(string commandPrefix)
        {
            return new EmbedBuilder()
                .WithDescription($"● For a full list of commands see the [website]({WebConstants.ReferenceUrl}).\n● Type `{commandPrefix}help command name` to see quick help for a specific command.\n\nIf you need further assistance or have any questions, please join the [support server]({WebConstants.SupportServerInvite}).")
                .WithTitle("❔ Help");
        }
    }
}
