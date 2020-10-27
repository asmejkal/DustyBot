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
    internal class CommandRouter : Events.EventHandler, ICommandRouter
    {
        private class CommandRegistrationFindResult
        {
            public string Prefix { get; }
            public CommandRegistration Registration { get; } 
            public CommandRegistration.Usage Usage { get; }

            public CommandRegistrationFindResult(string prefix, CommandRegistration registration, CommandRegistration.Usage usage)
            {
                Prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
                Registration = registration ?? throw new ArgumentNullException(nameof(registration));
                Usage = usage ?? throw new ArgumentNullException(nameof(usage));
            }
        }

        public IEnumerable<ICommandHandler> Handlers => _handlers;

        private readonly ICommunicator _communicator;
        private readonly ILogger _logger;
        private readonly FrameworkConfig _config;
        private readonly IFrameworkGuildConfigProvider _guildConfigProvider;
        private readonly HashSet<ICommandHandler> _handlers = new HashSet<ICommandHandler>();
        private readonly Dictionary<string, List<CommandRegistration>> _commandsMapping = new Dictionary<string, List<CommandRegistration>>();
        private readonly IUserFetcher _userFetcher;
        private int _commandCounter = 0;

        public CommandRouter(IEnumerable<ICommandHandler> handlers, ICommunicator communicator, ILogger logger, FrameworkConfig config, IFrameworkGuildConfigProvider guildConfigProvider, IUserFetcher userFetcher)
        {
            _communicator = communicator;
            _logger = logger;
            _config = config;
            _guildConfigProvider = guildConfigProvider;
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
                if (!(message is SocketUserMessage userMessage))
                    return;

                //Try to find a command registration for this message
                var findResult = await FindCommandRegistrationAsync(userMessage);
                if (findResult == null)
                    return;

                var stopwatch = Stopwatch.StartNew();
                var gatewayPing = DateTimeOffset.UtcNow - message.Timestamp;

                //Log; don't log command content for non-guild channels, since these commands are usually meant to be private
                var id = ++_commandCounter;
                var guild = (userMessage.Channel as IGuildChannel)?.Guild;
                if (guild != null)
                    await _logger.Log(new LogMessage(LogSeverity.Info, "Command", $"\"{message.Content}\"{(message.Attachments.Any() ? $" + {message.Attachments.Count} attachments" : "")} (id: {id}) by {message.Author.Username} ({message.Author.Id}) in #{message.Channel.Name} on {guild.Name} ({guild.Id})"));
                else
                    await _logger.Log(new LogMessage(LogSeverity.Info, "Command", $"\"{findResult.Prefix}{findResult.Usage.InvokeUsage}\"{(message.Attachments.Any() ? $" + {message.Attachments.Count} attachments" : "")} (id: {id}) by {message.Author.Username} ({message.Author.Id})"));

                //Check if the channel type is valid for this command
                if (!IsValidCommandSource(message.Channel, findResult.Registration))
                {
                    if (message.Channel is ITextChannel && findResult.Registration.Flags.HasFlag(CommandFlags.DirectMessageOnly))
                        await _communicator.CommandReplyDirectMessageOnly(message.Channel, findResult.Registration);

                    await _logger.Log(new LogMessage(LogSeverity.Debug, "Command", $"Command {id} rejected in {stopwatch.Elapsed.TotalSeconds:F3}s (g: {gatewayPing.TotalSeconds:F3}s) due to invalid channel type"));
                    return;
                }

                //Check owner
                if (findResult.Registration.Flags.HasFlag(CommandFlags.OwnerOnly) && !_config.OwnerIDs.Contains(message.Author.Id))
                {
                    await _communicator.CommandReplyNotOwner(message.Channel, findResult.Registration);
                    await _logger.Log(new LogMessage(LogSeverity.Debug, "Command", $"Command {id} rejected in {stopwatch.Elapsed.TotalSeconds:F3}s (g: {gatewayPing.TotalSeconds:F3}s) due to the user not being an owner"));
                    return;
                }

                //Check guild permisssions
                if (userMessage.Channel is IGuildChannel guildChannel)
                {
                    //User
                    var guildUser = message.Author as IGuildUser;
                    var missingPermissions = findResult.Registration.RequiredPermissions.Where(x => !guildUser.GuildPermissions.Has(x));
                    if (missingPermissions.Any())
                    {
                        await _communicator.CommandReplyMissingPermissions(message.Channel, findResult.Registration, missingPermissions);
                        await _logger.Log(new LogMessage(LogSeverity.Debug, "Command", $"Command {id} rejected in {stopwatch.Elapsed.TotalSeconds:F3}s (g: {gatewayPing.TotalSeconds:F3}s) due to missing user permissions"));
                        return;
                    }

                    //Bot
                    var selfUser = await guild.GetCurrentUserAsync();
                    var missingBotPermissions = findResult.Registration.BotPermissions.Where(x => !selfUser.GuildPermissions.Has(x));
                    if (missingBotPermissions.Any())
                    {
                        await _communicator.CommandReplyMissingBotPermissions(message.Channel, findResult.Registration, missingBotPermissions);
                        await _logger.Log(new LogMessage(LogSeverity.Debug, "Command", $"Command {id} rejected in {stopwatch.Elapsed.TotalSeconds:F3}s (g: {gatewayPing.TotalSeconds:F3}s) due to missing bot permissions"));
                        return;
                    }
                }

                var verificationElapsed = stopwatch.Elapsed;

                //Create command
                var parseResult = await SocketCommand.TryCreate(findResult.Registration, findResult.Usage, userMessage, findResult.Prefix, _userFetcher);
                if (parseResult.Item1.Type != SocketCommand.ParseResultType.Success)
                {
                    string explanation = string.Empty;
                    switch (parseResult.Item1.Type)
                    {
                        case SocketCommand.ParseResultType.NotEnoughParameters: explanation = Properties.Resources.Command_NotEnoughParameters; break;
                        case SocketCommand.ParseResultType.TooManyParameters: explanation = Properties.Resources.Command_TooManyParameters; break;
                        case SocketCommand.ParseResultType.InvalidParameterFormat: explanation = string.Format(Properties.Resources.Command_InvalidParameterFormat, ((SocketCommand.InvalidParameterParseResult)parseResult.Item1).InvalidPosition); break;
                    }

                    await _communicator.CommandReplyIncorrectParameters(message.Channel, findResult.Registration, explanation, findResult.Prefix);
                    await _logger.Log(new LogMessage(LogSeverity.Debug, "Command", $"Command {id} rejected in {stopwatch.Elapsed.TotalSeconds:F3}s (g: {gatewayPing.TotalSeconds:F3}s) due to incorrect parameters"));
                    return;
                }

                var command = parseResult.Item2;

                var parsingElapsed = stopwatch.Elapsed;

                //Execute
                var executor = new Func<Task>(async () =>
                {
                    IDisposable typingState = null;
                    if (findResult.Registration.Flags.HasFlag(CommandFlags.TypingIndicator))
                        typingState = command.Message.Channel.EnterTypingState();

                    bool succeeded = false;
                    try
                    {
                        await findResult.Registration.Handler(command);
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
                        await _communicator.CommandReplyIncorrectParameters(command.Message.Channel, findResult.Registration, ex.Message, findResult.Prefix, ex.ShowUsage);
                    }
                    catch (Exceptions.UnclearParametersCommandException ex)
                    {
                        await _communicator.CommandReplyUnclearParameters(command.Message.Channel, findResult.Registration, ex.Message, findResult.Prefix, ex.ShowUsage);
                    }
                    catch (Exceptions.MissingPermissionsException ex)
                    {
                        await _communicator.CommandReplyMissingPermissions(command.Message.Channel, findResult.Registration, ex.Permissions, ex.Message);
                    }
                    catch (Exceptions.MissingBotPermissionsException ex)
                    {
                        await _communicator.CommandReplyMissingBotPermissions(command.Message.Channel, findResult.Registration, ex.Permissions);
                    }
                    catch (Exceptions.MissingBotChannelPermissionsException ex)
                    {
                        await _communicator.CommandReplyMissingBotPermissions(command.Message.Channel, findResult.Registration, ex.Permissions);
                    }
                    catch (Exceptions.CommandException ex)
                    {
                        await _communicator.CommandReplyError(command.Message.Channel, ex.Message);
                    }
                    catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50001)
                    {
                        await _communicator.CommandReplyMissingBotAccess(command.Message.Channel, findResult.Registration);
                    }
                    catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50013)
                    {
                        await _communicator.CommandReplyMissingBotPermissions(command.Message.Channel, findResult.Registration);
                    }
                    catch (Exception ex)
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Error, "Internal",
                            $"Exception encountered while processing command {findResult.Registration.PrimaryUsage.InvokeUsage} in module {findResult.Registration.Handler.Target.GetType()}", ex));

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
                
                if (findResult.Registration.Flags.HasFlag(CommandFlags.Synchronous))
                    await executor();
                else
                    TaskHelper.FireForget(executor, x => _logger.Log(new LogMessage(LogSeverity.Error, "Internal", "Failure while handling command.", x)));
            }
            catch (Exception ex)
            {
                await _logger.Log(new LogMessage(LogSeverity.Error, "Internal", "Failed to process potential command message.", ex));
            }
        }

        private async Task<CommandRegistrationFindResult> FindCommandRegistrationAsync(SocketUserMessage userMessage)
        {
            var prefix = _config.DefaultCommandPrefix;
            if (userMessage.Channel is ITextChannel guildChannel)
            {
                var guildConfig = await _guildConfigProvider.GetConfigAsync(guildChannel.GuildId);
                if (!string.IsNullOrEmpty(guildConfig?.CustomCommandPrefix))
                    prefix = guildConfig.CustomCommandPrefix;
            }

            //Check prefix
            if (!userMessage.Content.StartsWith(prefix))
                return null;

            //Check if the message contains a command invoker
            var invoker = SocketCommand.ParseInvoker(userMessage.Content, prefix);
            if (string.IsNullOrEmpty(invoker))
                return null;

            //Try to find command registrations for this invoker
            List<CommandRegistration> commandRegistrations;
            if (!_commandsMapping.TryGetValue(invoker.ToLowerInvariant(), out commandRegistrations))
                return null;

            var match = SocketCommand.FindLongestMatch(userMessage.Content, prefix, commandRegistrations);
            if (match == null)
                return null;

            var (registration, usage) = match.Value;
            return new CommandRegistrationFindResult(prefix, registration, usage);
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
