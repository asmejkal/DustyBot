using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using System.Diagnostics;
using DustyBot.Core.Async;
using DustyBot.Core.Collections;
using DustyBot.Framework.Utility;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Config;

namespace DustyBot.Framework.Commands
{
    class CommandRouter : Events.EventHandler, ICommandRouter
    {
        public IEnumerable<ICommandHandler> Handlers => _handlers;

        private readonly ICommunicator _communicator;
        private readonly ILogger _logger;
        private readonly FrameworkConfig _config;
        private readonly HashSet<ICommandHandler> _handlers = new HashSet<ICommandHandler>();
        private readonly Dictionary<string, List<CommandRegistration>> _commandsMapping = new Dictionary<string, List<CommandRegistration>>();
        private readonly IUserFetcher _userFetcher;
        private int _commandCounter = 0;

        public CommandRouter(IEnumerable<ICommandHandler> handlers, ICommunicator communicator, ILogger logger, FrameworkConfig config, IUserFetcher userFetcher)
        {
            _communicator = communicator;
            _logger = logger;
            _config = config;
            _userFetcher = userFetcher;
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

                var stopwatch = Stopwatch.StartNew();
                var gatewayPing = DateTimeOffset.UtcNow - message.Timestamp;

                //Log; don't log command content for non-guild channels, since these commands are usually meant to be private
                var id = ++_commandCounter;
                var guild = (userMessage.Channel as IGuildChannel)?.Guild;
                if (guild != null)
                    await _logger.Log(new LogMessage(LogSeverity.Info, "Command", $"\"{message.Content}\"{(message.Attachments.Any() ? $" + {message.Attachments.Count} attachments" : "")} (id: {id}) by {message.Author.Username} ({message.Author.Id}) in #{message.Channel.Name} on {guild.Name} ({guild.Id})"));
                else
                    await _logger.Log(new LogMessage(LogSeverity.Info, "Command", $"\"{_config.CommandPrefix}{commandUsage.InvokeUsage}\"{(message.Attachments.Any() ? $" + {message.Attachments.Count} attachments" : "")} (id: {id}) by {message.Author.Username} ({message.Author.Id})"));

                //Check if the channel type is valid for this command
                if (!IsValidCommandSource(message.Channel, commandRegistration))
                {
                    if (message.Channel is ITextChannel && commandRegistration.Flags.HasFlag(CommandFlags.DirectMessageOnly))
                        await _communicator.CommandReplyDirectMessageOnly(message.Channel, commandRegistration);

                    await _logger.Log(new LogMessage(LogSeverity.Debug, "Command", $"Command {id} rejected in {stopwatch.Elapsed.TotalSeconds:F3}s (g: {gatewayPing.TotalSeconds:F3}s) due to invalid channel type"));
                    return;
                }

                //Check owner
                if (commandRegistration.Flags.HasFlag(CommandFlags.OwnerOnly) && !_config.OwnerIDs.Contains(message.Author.Id))
                {
                    await _communicator.CommandReplyNotOwner(message.Channel, commandRegistration);
                    await _logger.Log(new LogMessage(LogSeverity.Debug, "Command", $"Command {id} rejected in {stopwatch.Elapsed.TotalSeconds:F3}s (g: {gatewayPing.TotalSeconds:F3}s) due to the user not being an owner"));
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
                        await _communicator.CommandReplyMissingPermissions(message.Channel, commandRegistration, missingPermissions);
                        await _logger.Log(new LogMessage(LogSeverity.Debug, "Command", $"Command {id} rejected in {stopwatch.Elapsed.TotalSeconds:F3}s (g: {gatewayPing.TotalSeconds:F3}s) due to missing user permissions"));
                        return;
                    }

                    //Bot
                    var selfUser = await guild.GetCurrentUserAsync();
                    var missingBotPermissions = commandRegistration.BotPermissions.Where(x => !selfUser.GuildPermissions.Has(x));
                    if (missingBotPermissions.Any())
                    {
                        await _communicator.CommandReplyMissingBotPermissions(message.Channel, commandRegistration, missingBotPermissions);
                        await _logger.Log(new LogMessage(LogSeverity.Debug, "Command", $"Command {id} rejected in {stopwatch.Elapsed.TotalSeconds:F3}s (g: {gatewayPing.TotalSeconds:F3}s) due to missing bot permissions"));
                        return;
                    }
                }

                var verificationElapsed = stopwatch.Elapsed;

                //Create command
                var parseResult = await SocketCommand.TryCreate(commandRegistration, commandUsage, userMessage, _config.CommandPrefix, _userFetcher);
                if (parseResult.Item1.Type != SocketCommand.ParseResultType.Success)
                {
                    string explanation = string.Empty;
                    switch (parseResult.Item1.Type)
                    {
                        case SocketCommand.ParseResultType.NotEnoughParameters: explanation = Properties.Resources.Command_NotEnoughParameters; break;
                        case SocketCommand.ParseResultType.TooManyParameters: explanation = Properties.Resources.Command_TooManyParameters; break;
                        case SocketCommand.ParseResultType.InvalidParameterFormat: explanation = string.Format(Properties.Resources.Command_InvalidParameterFormat, ((SocketCommand.InvalidParameterParseResult)parseResult.Item1).InvalidPosition); break;
                    }

                    await _communicator.CommandReplyIncorrectParameters(message.Channel, commandRegistration, explanation);
                    await _logger.Log(new LogMessage(LogSeverity.Debug, "Command", $"Command {id} rejected in {stopwatch.Elapsed.TotalSeconds:F3}s (g: {gatewayPing.TotalSeconds:F3}s) due to incorrect parameters"));
                    return;
                }

                var command = parseResult.Item2;

                var parsingElapsed = stopwatch.Elapsed;

                //Execute
                var executor = new Func<Task>(async () =>
                {
                    IDisposable typingState = null;
                    if (commandRegistration.Flags.HasFlag(CommandFlags.TypingIndicator))
                        typingState = command.Message.Channel.EnterTypingState();

                    bool succeeded = false;
                    try
                    {
                        await commandRegistration.Handler(command);
                        succeeded = true;
                    }
                    catch (Exceptions.AbortException ex)
                    {
                        if (ex.Pages != null)
                            await _communicator.CommandReply(command.Message.Channel, ex.Pages);
                        else if (!string.IsNullOrEmpty(ex.Message))
                            await _communicator.CommandReply(command.Message.Channel, ex.Message);

                        succeeded = true;
                    }
                    catch (Exceptions.IncorrectParametersCommandException ex)
                    {
                        await _communicator.CommandReplyIncorrectParameters(command.Message.Channel, commandRegistration, ex.Message, ex.ShowUsage);
                    }
                    catch (Exceptions.UnclearParametersCommandException ex)
                    {
                        await _communicator.CommandReplyUnclearParameters(command.Message.Channel, commandRegistration, ex.Message, ex.ShowUsage);
                    }
                    catch (Exceptions.MissingPermissionsException ex)
                    {
                        await _communicator.CommandReplyMissingPermissions(command.Message.Channel, commandRegistration, ex.Permissions, ex.Message);
                    }
                    catch (Exceptions.MissingBotPermissionsException ex)
                    {
                        await _communicator.CommandReplyMissingBotPermissions(command.Message.Channel, commandRegistration, ex.Permissions);
                    }
                    catch (Exceptions.MissingBotChannelPermissionsException ex)
                    {
                        await _communicator.CommandReplyMissingBotPermissions(command.Message.Channel, commandRegistration, ex.Permissions);
                    }
                    catch (Exceptions.CommandException ex)
                    {
                        await _communicator.CommandReplyError(command.Message.Channel, ex.Message);
                    }
                    catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50001)
                    {
                        await _communicator.CommandReplyMissingBotAccess(command.Message.Channel, commandRegistration);
                    }
                    catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50013)
                    {
                        await _communicator.CommandReplyMissingBotPermissions(command.Message.Channel, commandRegistration);
                    }
                    catch (Exception ex)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, "Internal",
                            $"Exception encountered while processing command {commandRegistration.PrimaryUsage.InvokeUsage} in module {commandRegistration.Handler.Target.GetType()}", ex));

                        await _communicator.CommandReplyGenericFailure(command.Message.Channel);
                    }
                    finally
                    {
                        if (typingState != null)
                            typingState.Dispose();
                    }

                    var totalElapsed = stopwatch.Elapsed;
                    await _logger.Log(new LogMessage(LogSeverity.Debug, "Command", $"Command {id} {(succeeded ? "succeeded" : "failed")} in {totalElapsed.TotalSeconds:F3}s (v: {verificationElapsed.TotalSeconds:F3}s, p: {(parsingElapsed - verificationElapsed).TotalSeconds:F3}s, e: {(totalElapsed - parsingElapsed).TotalSeconds:F3}s, g: {gatewayPing.TotalSeconds:F3}s)"));
                });
                
                if (commandRegistration.Flags.HasFlag(CommandFlags.Synchronous))
                    await executor();
                else
                    TaskHelper.FireForget(executor, x => _logger.Log(new LogMessage(LogSeverity.Error, "Internal", "Failure while handling command.", x)));
            }
            catch (Exception ex)
            {
                await _logger.Log(new LogMessage(LogSeverity.Error, "Internal", "Failed to process potential command message.", ex));
            }
        }

        private (CommandRegistration registration, CommandRegistration.Usage usage)? TryGetCommandRegistration(SocketUserMessage userMessage)
        {
            //Check prefix
            if (!userMessage.Content.StartsWith(_config.CommandPrefix))
                return null;

            //Check if the message contains a command invoker
            var invoker = SocketCommand.ParseInvoker(userMessage.Content, _config.CommandPrefix);
            if (string.IsNullOrEmpty(invoker))
                return null;

            //Try to find command registrations for this invoker
            List<CommandRegistration> commandRegistrations;
            if (!_commandsMapping.TryGetValue(invoker.ToLowerInvariant(), out commandRegistrations))
                return null;

            return SocketCommand.FindLongestMatch(userMessage.Content, _config.CommandPrefix, commandRegistrations);
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
