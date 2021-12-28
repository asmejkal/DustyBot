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
    }
}
