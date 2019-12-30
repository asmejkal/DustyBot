using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using DustyBot.Framework.Utility;
using System.Text.RegularExpressions;

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
                foreach (var usage in command.EveryUsage)
                {
                    if (!_commandsMapping.TryGetValue(usage.InvokeString.ToLowerInvariant(), out var commands))
                        _commandsMapping.Add(usage.InvokeString.ToLowerInvariant(), commands = new List<CommandRegistration>());

                    commands.Add(command);
                }                
            }
        }

        public override async Task OnMessageReceived(SocketMessage message)
        {
            try
            {
                //Filter messages from bots
                if (message.Author.IsBot)
                    return;

                //Filter anything but normal user messages
                var userMessage = message as SocketUserMessage;
                if (userMessage == null)
                    return;

                //Try to find a command registration for this message
                var findResult = TryGetCommandRegistration(userMessage);
                if (findResult == null)
                    return;

                var (commandRegistration, commandUsage) = findResult.Value;

                //Log; don't log command content for non-guild channels, since these commands are usually meant to be private
                var guild = (userMessage.Channel as IGuildChannel)?.Guild;
                if (guild != null)
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Command", $"\"{message.Content}\" by {message.Author.Username} ({message.Author.Id}) in #{message.Channel.Name} on {guild.Name} ({guild.Id})"));
                else
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Command", $"\"{Config.CommandPrefix}{commandUsage.InvokeUsage}\" by {message.Author.Username} ({message.Author.Id})"));

                //Check if the channel type is valid for this command
                if (!IsValidCommandSource(message.Channel, commandRegistration))
                {
                    if (message.Channel is ITextChannel && commandRegistration.Flags.HasFlag(CommandFlags.DirectMessageOnly))
                        await Communicator.CommandReplyDirectMessageOnly(message.Channel, commandRegistration);

                    return;
                }

                //Check owner
                if (commandRegistration.Flags.HasFlag(CommandFlags.OwnerOnly) && !Config.OwnerIDs.Contains(message.Author.Id))
                {
                    await Communicator.CommandReplyNotOwner(message.Channel, commandRegistration);
                    return;
                }

                //Check guild permisssions
                if (userMessage.Channel is IGuildChannel guildChannel)
                {
                    //User
                    var guildUser = message.Author as IGuildUser;
                    var missingPermissions = commandRegistration.RequiredPermissions.Where(x => !guildUser.GuildPermissions.Has(x));
                    if (missingPermissions.Any())
                    {
                        await Communicator.CommandReplyMissingPermissions(message.Channel, commandRegistration, missingPermissions);
                        return;
                    }

                    //Bot
                    var selfUser = await guild.GetCurrentUserAsync();
                    var missingBotPermissions = commandRegistration.BotPermissions.Where(x => !selfUser.GuildPermissions.Has(x));
                    if (missingBotPermissions.Any())
                    {
                        await Communicator.CommandReplyMissingBotPermissions(message.Channel, commandRegistration, missingBotPermissions);
                        return;
                    }
                }

                //Create command
                var parseResult = await SocketCommand.TryCreate(commandRegistration, commandUsage, userMessage, Config);
                if (parseResult.Item1.Type != SocketCommand.ParseResultType.Success)
                {
                    string explanation = string.Empty;
                    switch (parseResult.Item1.Type)
                    {
                        case SocketCommand.ParseResultType.NotEnoughParameters: explanation = Properties.Resources.Command_NotEnoughParameters; break;
                        case SocketCommand.ParseResultType.TooManyParameters: explanation = Properties.Resources.Command_TooManyParameters; break;
                        case SocketCommand.ParseResultType.InvalidParameterFormat: explanation = string.Format(Properties.Resources.Command_InvalidParameterFormat, ((SocketCommand.InvalidParameterParseResult)parseResult.Item1).InvalidPosition); break;
                    }

                    await Communicator.CommandReplyIncorrectParameters(message.Channel, commandRegistration, explanation);
                    return;
                }

                var command = parseResult.Item2;

                //Execute
                var executor = new Func<Task>(async () =>
                {
                    IDisposable typingState = null;
                    if (commandRegistration.Flags.HasFlag(CommandFlags.TypingIndicator))
                        typingState = command.Message.Channel.EnterTypingState();

                    try
                    {
                        await commandRegistration.Handler(command);
                    }
                    catch (Exceptions.IncorrectParametersCommandException ex)
                    {
                        await Communicator.CommandReplyIncorrectParameters(command.Message.Channel, commandRegistration, ex.Message, ex.ShowUsage);
                    }
                    catch (Exceptions.UnclearParametersCommandException ex)
                    {
                        await Communicator.CommandReplyUnclearParameters(command.Message.Channel, commandRegistration, ex.Message, ex.ShowUsage);
                    }
                    catch (Exceptions.MissingPermissionsException ex)
                    {
                        await Communicator.CommandReplyMissingPermissions(command.Message.Channel, commandRegistration, ex.Permissions, ex.Message);
                    }
                    catch (Exceptions.MissingBotPermissionsException ex)
                    {
                        await Communicator.CommandReplyMissingBotPermissions(command.Message.Channel, commandRegistration, ex.Permissions);
                    }
                    catch (Exceptions.MissingBotChannelPermissionsException ex)
                    {
                        await Communicator.CommandReplyMissingBotPermissions(command.Message.Channel, commandRegistration, ex.Permissions);
                    }
                    catch (Exceptions.AbortException ex)
                    {
                        if (ex.Pages != null)
                            await Communicator.CommandReply(command.Message.Channel, ex.Pages);
                        else
                            await Communicator.CommandReply(command.Message.Channel, ex.Message);
                    }
                    catch (Exceptions.CommandException ex)
                    {
                        await Communicator.CommandReplyError(command.Message.Channel, ex.Message);
                    }
                    catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50001)
                    {
                        await Communicator.CommandReplyMissingBotAccess(command.Message.Channel, commandRegistration);
                    }
                    catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50013)
                    {
                        await Communicator.CommandReplyMissingBotPermissions(command.Message.Channel, commandRegistration);
                    }
                    catch (Exception ex)
                    {
                        await Logger.Log(new LogMessage(LogSeverity.Error, "Internal",
                            $"Exception encountered while processing command {commandRegistration.PrimaryUsage.InvokeUsage} in module {commandRegistration.Handler.Target.GetType()}", ex));

                        await Communicator.CommandReplyGenericFailure(command.Message.Channel, commandRegistration);
                    }
                    finally
                    {
                        if (typingState != null)
                            typingState.Dispose();
                    }
                });
                
                if (commandRegistration.Flags.HasFlag(CommandFlags.RunAsync))
                    TaskHelper.FireForget(executor, x => Logger.Log(new LogMessage(LogSeverity.Error, "Internal", "Failure while handling command.", x)));
                else
                    await executor();
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Internal", "Failed to process potential command message.", ex));
            }
        }

        private (CommandRegistration registration, CommandRegistration.Usage usage)? TryGetCommandRegistration(SocketUserMessage userMessage)
        {
            //Check prefix
            if (!userMessage.Content.StartsWith(Config.CommandPrefix))
                return null;

            //Check if the message contains a command invoker
            var invoker = SocketCommand.ParseInvoker(userMessage.Content, Config.CommandPrefix);
            if (string.IsNullOrEmpty(invoker))
                return null;

            //Try to find command registrations for this invoker
            List<CommandRegistration> commandRegistrations;
            if (!_commandsMapping.TryGetValue(invoker.ToLowerInvariant(), out commandRegistrations))
                return null;

            return SocketCommand.FindLongestMatch(userMessage.Content, Config.CommandPrefix, commandRegistrations);
        }

        private bool IsValidCommandSource(IMessageChannel channel, CommandRegistration cr)
        {
            if (channel is ITextChannel && !cr.Flags.HasFlag(CommandFlags.DirectMessageOnly))
                return true;

            if (channel is IDMChannel && (cr.Flags.HasFlag(CommandFlags.DirectMessageAllow) || cr.Flags.HasFlag(CommandFlags.DirectMessageOnly)))
                return true;

            return false;
        }
    }
}
