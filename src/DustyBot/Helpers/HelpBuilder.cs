using Discord;
using DustyBot.Definitions;
using DustyBot.Framework.Utility;
using System.Text;

namespace DustyBot.Helpers
{
    internal static class HelpBuilder
    {
        public static string GetModuleWebLink(string name) => 
            DiscordHelpers.SanitiseMarkdownUri(WebConstants.ReferenceUrl + "#" + WebConstants.GetModuleWebAnchor(name));

        public static Embed GetModuleHelpEmbed(string name, string commandPrefix)
        {
            return new EmbedBuilder()
                .WithDescription($"● For a list of commands in this module, please see the [website]({GetModuleWebLink(name)}).\n● Type `{commandPrefix}help command name` to see quick help for a specific command.\n\nIf you need further assistance or have any questions, please join the [support server]({WebConstants.SupportServerInvite}).")
                .WithTitle($"❔ {name} help").Build();
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
