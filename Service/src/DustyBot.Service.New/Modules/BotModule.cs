using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Disqord;
using Disqord.Bot;
using DustyBot.Core.Formatting;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Modules;
using DustyBot.Service.Services.Bot;
using Qmmands;
using Qmmands.Text;
using Disqord.Bot.Commands;
using Qommon.Metadata;

namespace DustyBot.Service.Modules
{
    [Name("Bot"), Description("Help and bot-related commands.")]
    public class BotModule : DustyModuleBase
    {
        private readonly HelpBuilder _helpBuilder;
        private readonly WebLinkResolver _webLinkResolver;

        public BotModule(HelpBuilder helpBuilder, WebLinkResolver webLinkResolver)
        {
            _helpBuilder = helpBuilder ?? throw new ArgumentNullException(nameof(helpBuilder));
            _webLinkResolver = webLinkResolver ?? throw new ArgumentNullException(nameof(webLinkResolver));
        }

        [TextCommand("help"), Description("Shows how to use a command.")]
        [Example("event add")]
        public IDiscordCommandResult ShowHelp(
            [Description("show usage for a command")]
            [Remainder]
            string? command)
        {
            if (command == default)
            {
                return Success(); // Reply(_helpBuilder.BuildHelpEmbed(Context.Prefix)); // TODO
            }
            else
            {
                var match = Bot.Commands.GetCommandMapProvider().GetRequiredMap<ITextCommandMap>().FindBestMatch(command.AsMemory());
                if (match == default)
                    return Success(); // Failure("Can't find this command."); TODO

                return Result(_helpBuilder.BuildCommandHelpEmbed(match.Command, Context.Prefix));
            }
        }

        [VerbCommand("help", "dump"), Description("Generates a list of all commands.")]
        [RequireBotOwner]
        public IDiscordCommandResult DumpHelp()
        {
            var result = new StringBuilder();
            var preface = new StringBuilder("<div class=\"row\"><div class=\"col-lg-12 section-heading\" style=\"margin-bottom: 0px\">\n<h3><img class=\"feature-icon-big\" src=\"img/compass.png\"/>Quick navigation</h3>\n");
            foreach (var module in Bot.Commands.EnumerateModules().SelectMany(x => x.Value).Where(x => !x.IsHidden()))
            {
                var anchor = WebLinkResolver.GetModuleWebAnchor(module.Name);
                var description = ConvertToHtml(module.Description);
                preface.AppendLine($"<p class=\"text-muted\"><a href=\"#{anchor}\"><img class=\"feature-icon-small\" src=\"img/modules/{module.Name}.png\"/>{module.Name}</a> – {description}</p>");

                result.AppendLine($"<div class=\"row\"><div class=\"col-lg-12\"><a class=\"anchor\" id=\"{anchor}\"></a><h3><img class=\"feature-icon-big\" src=\"img/modules/{module.Name}.png\"/>{module.Name}</h3>");

                var commands = module.Commands.Cast<ITextCommand>()
                    .Concat(module.Submodules.Where(x => string.IsNullOrEmpty(x.Description))
                    .SelectMany(x => GetAllCommands(x)))
                    .ToList();

                if (commands.Any())
                    result.AppendLine(BuildCommandList(module.Description, commands));

                foreach (var submodule in module.Submodules.Where(x => !string.IsNullOrEmpty(x.Description)))
                    result.AppendLine(BuildCommandList(submodule.Description, GetAllCommands(submodule)));

                result.AppendLine("</div></div>");
            }

            preface.AppendLine("</div></div>");
            preface.AppendLine("<hr/>");
            preface.Append(result);

            var file = Encoding.UTF8.GetBytes(preface.ToString());
            return Success(new LocalMessage().WithAttachments(LocalAttachment.Bytes(file, "output.html")));
        }

        private IEnumerable<ITextCommand> GetAllCommands(IModule module)
        {
            var result = module.Commands.Cast<ITextCommand>();
            foreach (var submodule in module.Submodules)
                result = result.Concat(GetAllCommands(submodule));

            return result;
        }

        private static string ConvertToHtml(string input)
        {
            bool inside = false;
            input = input.Split('`').Aggregate((x, y) => x + ((inside = !inside) ? "<span class=\"param\">" : "</span>") + y);

            inside = false;
            input = input.Split(new string[] { "**" }, StringSplitOptions.None).Aggregate((x, y) => x + ((inside = !inside) ? "<b>" : "</b>") + y);

            inside = false;
            input = input.Split(new string[] { "*" }, StringSplitOptions.None).Aggregate((x, y) => x + ((inside = !inside) ? "<i>" : "</i>") + y);

            inside = false;
            input = input.Split(new string[] { "__" }, StringSplitOptions.None).Aggregate((x, y) => x + ((inside = !inside) ? "<u>" : "</u>") + y);

            input = input.Replace("\r\n", "<br/>");
            input = input.Replace("\n", "<br/>");

            return input;
        }

        private static string BuildCommandList(string description, IEnumerable<ITextCommand> commands)
        {
            var result = new StringBuilder();
            result.AppendLine($"<p class=\"text-muted\">{description}</p>");
            foreach (var command in commands.Where(x => !x.IsHidden()))
            {
                var id = Guid.NewGuid().ToString("N");
                result.AppendLine($"<p data-target=\"#{id}\" data-toggle=\"collapse\" class=\"paramlistitem\">" +
                    $"<i class=\"fa fa-angle-right\" style=\"margin-right: 3px;\"></i><span class=\"paramlistcode\">{command.EnumerateFullAliases().First()}</span> – {command.Description} " +
                    "</p>");

                var usage = BuildWebUsageString(command, ">");
                if (string.IsNullOrEmpty(usage))
                    continue;

                result.AppendLine($"<div id=\"{id}\" class=\"collapse usage\">");
                result.Append(usage);
                result.AppendLine("</div>");
            }

            result.Append("</br>");
            return result.ToString();
        }

        private static string BuildWebUsageString(ITextCommand command, string commandPrefix)
        {
            string usage = $"{commandPrefix}{command.EnumerateFullAliases().First()}";
            foreach (var param in command.Parameters.Where(x => !x.IsHidden()))
            {
                string tmp = param.Name.Capitalize();
                if (param is IPositionalParameter { IsRemainder: true })
                    tmp += "...";

                if (param.HasDefaultValue())
                    tmp = $"[{tmp}]";

                usage += $" <span class=\"param\">{tmp}</span>";
            }

            var paramDescriptions = new StringBuilder();
            foreach (var param in command.Parameters.Where(x => !x.IsHidden() && !string.IsNullOrEmpty(x.Description)))
            {
                string tmp = $"● `{param.Name.Capitalize()}` ‒ ";
                if (param.HasDefaultValue())
                    tmp += "optional; ";

                tmp += param.Description;
                paramDescriptions.Append(paramDescriptions.Length <= 0 ? tmp : "<br/>" + tmp);
            }

            var examples = command.GetExamples()
                .Select(x => $"{commandPrefix}{command.EnumerateFullAliases().First()} {ConvertToHtml(x)}")
                .DefaultIfEmpty()
                .Aggregate((x, y) => x + "<br/>" + y);

            var result = new StringBuilder($"<span class=\"usagecode\">{usage}</span>");
            if (paramDescriptions.Length > 0)
                result.Append("<br/><br/>" + ConvertToHtml(paramDescriptions.ToString()));

            var remarks = command.GetMetadataOrDefault<string>(MetadataKeys.Remarks);
            if (!string.IsNullOrWhiteSpace(remarks))
                result.Append("<br/><br/>" + ConvertToHtml(remarks));

            if (!string.IsNullOrWhiteSpace(examples))
                result.Append("<br/><br/><u>Examples:</u><br/><code>" + ConvertToHtml(examples) + "</code>");

            if (command.EnumerateFullAliases().Skip(1).Any())
            {
                result.Append("<br/><span class=\"aliases\">Also as "
                    + command.EnumerateFullAliases().Skip(1).Select(x => $"<span class=\"alias\">{x}</span>").WordJoin(lastSeparator: " or ")
                    + "</span>");
            }

            return result.ToString();
        }
    }
}
