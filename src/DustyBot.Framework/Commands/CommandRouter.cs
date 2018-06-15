using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using DustyBot.Framework.Utility;

namespace DustyBot.Framework.Commands
{
    class CommandRouter : Events.EventHandler, ICommandRouter
    {
        public IEnumerable<ICommandHandler> Handlers => _handlers;

        HashSet<ICommandHandler> _handlers = new HashSet<ICommandHandler>();
        Dictionary<string, List<CommandRegistration>> _commandsMapping = new Dictionary<string, List<CommandRegistration>>();

        public Communication.ICommunicator Communicator { get; set; }
        public Logging.ILogger Logger { get; set; }
        public Config.IEssentialConfig Config { get; set; }

        public CommandRouter(IEnumerable<ICommandHandler> handlers, Communication.ICommunicator communicator, Logging.ILogger logger, Config.IEssentialConfig config)
        {
            Communicator = communicator;
            Logger = logger;
            Config = config;

            handlers.ForEach(x => Register(x));
        }

        public void Register(ICommandHandler handler)
        {
            _handlers.Add(handler);

            foreach (var command in handler.HandledCommands)
            {
                List<CommandRegistration> commands;
                if (!_commandsMapping.TryGetValue(command.InvokeString.ToLowerInvariant(), out commands))
                    _commandsMapping.Add(command.InvokeString.ToLowerInvariant(), commands = new List<CommandRegistration>());

                commands.Add(command);
            }
        }

        public override async Task OnMessageReceived(SocketMessage message)
        {
            //Filter messages from bots
            if (message.Author.IsBot)
                return;

            //Filter anything but normal user messages
            var userMessage = message as SocketUserMessage;
            if (userMessage == null)
                return;

            //Only accept messages from guild users
            var guildUser = userMessage.Author as SocketGuildUser;
            if (guildUser == null)
                return;

            //Check if the message is a command
            ICommand command;
            if (!SocketCommand.TryCreate(userMessage, Config, out command))
                return;

            //Try to find command registrations for this invoker
            List<CommandRegistration> commandRegistrations;
            if (!_commandsMapping.TryGetValue(command.Invoker.ToLowerInvariant(), out commandRegistrations))
                return;

            await Logger.Log(new LogMessage(LogSeverity.Info, "Command", "\"" + message.Content + "\" by " + userMessage.Author.Username + " (" + userMessage.Author.Id + ")"));

            //Send the command to all registered modules
            foreach (var commandRegistration in commandRegistrations)
            {
                //Check permisssions
                var missingPermissions = commandRegistration.RequiredPermissions.Where(x => !guildUser.GuildPermissions.Has(x));
                if (missingPermissions.Count() > 0)
                {
                    await Communicator.CommandReplyMissingPermissions(command.Message.Channel, commandRegistration, missingPermissions);
                    continue;
                }

                //Check expected parameters
                if (!CheckRequiredParameters(command, commandRegistration))
                {
                    await Communicator.CommandReplyIncorrectParameters(command.Message.Channel, commandRegistration, "");
                    continue;
                }                    

                var executor = new Func<Task>(async () =>
                {
                    try
                    {
                        //Execute
                        await commandRegistration.Handler(command);
                    }
                    catch (Exceptions.IncorrectParametersCommandException ex)
                    {
                        await Communicator.CommandReplyIncorrectParameters(command.Message.Channel, commandRegistration, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        await Logger.Log(new LogMessage(LogSeverity.Error, "Internal",
                            $"Exception encountered while processing command {commandRegistration.InvokeString} in module {commandRegistration.Handler.Target.GetType()}", ex));

                        await Communicator.CommandReplyGenericFailure(command.Message.Channel, commandRegistration);
                    }
                });

                if (commandRegistration.RunAsync)
                    TaskHelper.FireForget(executor);
                else
                    await executor();
            }
        }

        private bool CheckRequiredParameters(ICommand command, CommandRegistration commandRegistration)
        {
            if (command.ParametersCount < commandRegistration.RequiredParameters.Count)
                return false;

            for (int i = 0; i < commandRegistration.RequiredParameters.Count; ++i)
            {
                if (!command.GetParameter(i).IsType(commandRegistration.RequiredParameters[i]))
                    return false;
            }

            return true;
        }
    }
}
