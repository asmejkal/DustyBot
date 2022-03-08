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

        [Command("help"), Description("Shows how to use a command.")]
        [Example("event add")]
        public CommandResult ShowHelp(
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
                var match = Bot.Commands.FindCommands(command).FirstOrDefault();
                if (match == default)
                    return Success(); // Failure("Can't find this command."); TODO

                return Result(_helpBuilder.BuildCommandHelpEmbed(match.Command, Context.Prefix));
            }
        }

        [VerbCommand("help", "dump"), Description("Generates a list of all commands.")]
        [RequireBotOwner]
        public CommandResult DumpHelp()
        {
            var result = new StringBuilder();
            var preface = new StringBuilder("<div class=\"row\"><div class=\"col-lg-12 section-heading\" style=\"margin-bottom: 0px\">\n<h3><img class=\"feature-icon-big\" src=\"img/compass.png\"/>Quick navigation</h3>\n");
            foreach (var module in Bot.Commands.TopLevelModules.Where(x => !x.IsHidden()))
            {
                var anchor = WebLinkResolver.GetModuleWebAnchor(module.Name);
                var description = ConvertToHtml(module.Description);
                preface.AppendLine($"<p class=\"text-muted\"><a href=\"#{anchor}\"><img class=\"feature-icon-small\" src=\"img/modules/{module.Name}.png\"/>{module.Name}</a> – {description}</p>");

                result.AppendLine($"<div class=\"row\"><div class=\"col-lg-12\"><a class=\"anchor\" id=\"{anchor}\"></a><h3><img class=\"feature-icon-big\" src=\"img/modules/{module.Name}.png\"/>{module.Name}</h3>");

                var commands = module.Commands
                    .Concat(module.Submodules.Where(x => string.IsNullOrEmpty(x.Description))
                    .SelectMany(x => GetAllCommands(x)))
                    .ToList();

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

        private IEnumerable<Command> GetAllCommands(Module module)
        {
            var result = (IEnumerable<Command>)module.Commands;
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

        private static string BuildCommandList(string description, IEnumerable<Command> commands)
        {
            var result = new StringBuilder();
            result.AppendLine($"<p class=\"text-muted\">{description}</p>");
            foreach (var command in commands.Where(x => !x.IsHidden()))
            {
                var id = Guid.NewGuid().ToString("N");
                result.AppendLine($"<p data-target=\"#{id}\" data-toggle=\"collapse\" class=\"paramlistitem\">" +
                    $"<i class=\"fa fa-angle-right\" style=\"margin-right: 3px;\"></i><span class=\"paramlistcode\">{command.FullAliases.First()}</span> – {command.Description} " +
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

        private static string BuildWebUsageString(Command command, string commandPrefix)
        {
            string usage = $"{commandPrefix}{command.FullAliases.First()}";
            foreach (var param in command.Parameters.Where(x => !x.IsHidden()))
            {
                string tmp = param.Name.Capitalize();
                if (param.IsRemainder)
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
                .Select(x => $"{commandPrefix}{command.FullAliases.First()} {ConvertToHtml(x)}")
                .DefaultIfEmpty()
                .Aggregate((x, y) => x + "<br/>" + y);

            var result = new StringBuilder($"<span class=\"usagecode\">{usage}</span>");
            if (paramDescriptions.Length > 0)
                result.Append("<br/><br/>" + ConvertToHtml(paramDescriptions.ToString()));

            if (!string.IsNullOrWhiteSpace(command.Remarks))
                result.Append("<br/><br/>" + ConvertToHtml(command.Remarks));

            if (!string.IsNullOrWhiteSpace(examples))
                result.Append("<br/><br/><u>Examples:</u><br/><code>" + ConvertToHtml(examples) + "</code>");

            if (command.FullAliases.Skip(1).Any())
            {
                result.Append("<br/><span class=\"aliases\">Also as "
                    + command.FullAliases.Skip(1).Select(x => $"<span class=\"alias\">{x}</span>").WordJoin(lastSeparator: " or ")
                    + "</span>");
            }

            return result.ToString();
        }
    }
}
