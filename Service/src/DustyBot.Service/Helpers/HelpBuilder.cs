using System;
using System.Text;
using Discord;
using Disqord;
using DustyBot.Framework.Utility;
using DustyBot.Service.Configuration;
using DustyBot.Service.Definitions;
using Microsoft.Extensions.Options;

namespace DustyBot.Service.Helpers
{
    internal class HelpBuilder
    {
        private readonly WebsiteWalker _websiteWalker;
        private readonly IOptions<WebOptions> _webOptions;

        public HelpBuilder(WebsiteWalker websiteWalker, IOptions<WebOptions> webOptions)
        {
            _websiteWalker = websiteWalker;
            _webOptions = webOptions;
        }

        public string GetModuleWebLink(string name) => 
            DiscordHelpers.SanitiseMarkdownUri(Uri.EscapeUriString _websiteWalker.ReferenceUrl + "#" + WebsiteWalker.GetModuleWebAnchor(name));

        public Embed GetModuleHelpEmbed(string name, string commandPrefix)
        {
            return new EmbedBuilder()
                .WithDescription($"● For a list of commands in this module, please see the [website]({GetModuleWebLink(name)}).\n● Type `{commandPrefix}help command name` to see quick help for a specific command.\n\nIf you need further assistance or have any questions, please join the [support server]({_webOptions.Value.SupportServerInvite}).")
                .WithTitle($"❔ {name} help").Build();
        }

        public EmbedBuilder GetHelpEmbed(string commandPrefix, bool customPrefix = false)
        {
            var description = new StringBuilder();
            if (customPrefix)
                description.AppendLine($"● The command prefix is `{commandPrefix}` in this guild.");

            description.AppendLine($"● For a full list of commands see the [website]({_websiteWalker.ReferenceUrl}).");
            description.AppendLine($"● Type `{commandPrefix}help command name` to see quick help for a specific command.");
            description.AppendLine();
            description.AppendLine($"If you need further assistance or have any questions, please join the [support server]({_webOptions.Value.SupportServerInvite}).");

            return new EmbedBuilder()
                .WithDescription(description.ToString())
                .WithTitle("❔ Help");
        }
    }
}
