using System;
using System.Linq;
using System.Text;
using Disqord;
using Disqord.Bot;
using DustyBot.Core.Formatting;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Utility;
using DustyBot.Service.Configuration;
using Microsoft.Extensions.Options;
using Qmmands;

namespace DustyBot.Service.Services.Bot
{
    public class HelpBuilder
    {
        public Uri SupportServerInvite => new Uri(_webOptions.SupportServerInvite ?? throw new InvalidOperationException("Not configured"));

        private readonly WebLinkResolver _webLinkResolver;
        private readonly WebOptions _webOptions;

        public HelpBuilder(WebLinkResolver webLinkResolver, IOptions<WebOptions> webOptions)
        {
            _webLinkResolver = webLinkResolver;
            _webOptions = webOptions.Value;
        }

        public LocalEmbed BuildHelpEmbed(IPrefix commandPrefix, bool showCustomPrefix = false)
        {
            var description = new StringBuilder();
            if (showCustomPrefix)
                description.AppendLine($"● The command prefix is `{commandPrefix}` in this guild.");

            description.AppendLine($"● For a list of commands see the {_webLinkResolver.Reference.ToMarkdown("website")}.");
            description.AppendLine($"● Type `{commandPrefix}help command name` to see quick help for a specific command.");
            description.AppendLine();
            description.AppendLine($"If you need further assistance or have any questions, please join the {SupportServerInvite.ToMarkdown("support server")}.");

            return new LocalEmbed()
                .WithDescription(description.ToString())
                .WithTitle($"{CommunicationConstants.QuestionMarker} Help");
        }

        public LocalEmbed BuildModuleHelpEmbed(string name, IPrefix commandPrefix)
        {
            return new LocalEmbed()
                .WithTitle($"{CommunicationConstants.QuestionMarker} {name} help")
                .WithDescription($"● For a list of commands in this module, please see the {_webLinkResolver.GetModuleReference(name).ToMarkdown("website")}.\n"
                    + $"● Type `{commandPrefix}help command name` to see quick help for a specific command.\n\n"
                    + $"If you need further assistance or have any questions, please join the {SupportServerInvite.ToMarkdown("support server")}.");
        }

        public LocalEmbed BuildCommandHelpEmbed(Command command, IPrefix commandPrefix)
        {
            var embed = new LocalEmbed()
                    .WithTitle($"Command {command.FullAliases.First()}")
                    .WithDescription(command.Description)
                    .WithFooter("If a parameter contains spaces, add quotes: \"he llo\". Parameters marked \"...\" need no quotes.");

            embed.AddField("Usage", BuildCommandUsage(command, commandPrefix));
            return embed;
        }

        public string BuildCommandUsage(Command command, IPrefix commandPrefix)
        {
            var usage = new StringBuilder($"{commandPrefix}{command.FullAliases.First()}");
            foreach (var param in command.Parameters.Where(x => !x.IsHidden()))
            {
                var name = param.Name.Capitalize();
                if (param.IsRemainder)
                    name += "...";

                if (param.HasDefaultValue())
                    name = $"[{name}]";

                usage.Append($" `{name}`");
            }

            usage.Append("\n\n");

            var parameters = new StringBuilder();
            foreach (var param in command.Parameters.Where(x => !x.IsHidden() && !string.IsNullOrWhiteSpace(x.Description)))
            {
                if (parameters.Length > 0)
                    parameters.AppendLine();

                parameters.Append($"● `{param.Name.Capitalize()}` ‒ ");
                if (param.HasDefaultValue())
                    parameters.Append("optional; ");

                parameters.Append(param.Description);
            }

            if (parameters.Length > 0)
                usage.Append(parameters + "\n\n");

            if (!string.IsNullOrEmpty(command.Remarks))
                usage.Append(command.Remarks + "\n\n");

            var examples = new StringBuilder();
            foreach (var example in command.GetExamples().Select(x => $"{commandPrefix}{command.FullAliases.First()} {x}"))
            {
                if (examples.Length > 0)
                    examples.AppendLine();

                examples.AppendLine(example);
            }

            if (examples.Length > 0)
                usage.Append("__Examples:__\n" + parameters + "\n\n");

            return usage.ToString();
        }

        public LocalEmbed BuildCommandUsageEmbed(Command command, IPrefix commandPrefix)
        {
            return new LocalEmbed()
                .WithTitle("Command usage")
                .WithDescription(BuildCommandUsage(command, commandPrefix));
        }
    }
}
