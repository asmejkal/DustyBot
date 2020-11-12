using Discord;
using DustyBot.Definitions;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Utility;
using System.Text;

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

        public static EmbedBuilder GetHelpEmbed(string commandPrefix, bool customPrefix = false)
        {
            var description = new StringBuilder();
            if (customPrefix)
                description.AppendLine($"● The command prefix is `{commandPrefix}` in this guild.");

            description.AppendLine($"● For a full list of commands see the [website]({WebConstants.ReferenceUrl}).");
            description.AppendLine($"● Type `{commandPrefix}help command name` to see quick help for a specific command.");
            description.AppendLine();
            description.AppendLine($"If you need further assistance or have any questions, please join the [support server]({WebConstants.SupportServerInvite}).");

            return new EmbedBuilder()
                .WithDescription(description.ToString())
                .WithTitle("❔ Help");
        }
    }
}
