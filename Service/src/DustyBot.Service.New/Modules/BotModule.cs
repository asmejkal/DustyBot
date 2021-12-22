using System;
using System.Linq;
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

        public BotModule(HelpBuilder helpBuilder)
        {
            _helpBuilder = helpBuilder ?? throw new ArgumentNullException(nameof(helpBuilder));
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
                return Reply(_helpBuilder.BuildHelpEmbed(Context.Prefix));
            }
            else
            {
                var match = Bot.Commands.FindCommands(command).FirstOrDefault();
                if (match == default)
                    return Failure("Can't find this command.");

                return Reply(_helpBuilder.BuildCommandHelpEmbed(match.Command, Context.Prefix));
            }
        }
    }
}
