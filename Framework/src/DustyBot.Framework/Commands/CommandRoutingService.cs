using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Core.Async;
using DustyBot.Framework.Commands.Parsing;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Configuration;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Commands
{
    internal sealed class CommandRoutingService : IDisposable
    {
        private enum CommandResult
        {
            Succeeded,
            Failed
        }

        private class CommandRegistrationFindResult
        {
            public string Prefix { get; }
            public CommandInfo Registration { get; } 
            public CommandInfo.Usage Usage { get; }

            public CommandRegistrationFindResult(string prefix, CommandInfo registration, CommandInfo.Usage usage)
            {
                Prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
                Registration = registration ?? throw new ArgumentNullException(nameof(registration));
                Usage = usage ?? throw new ArgumentNullException(nameof(usage));
            }
        }

        private readonly BaseSocketClient _client;
        private readonly IServiceProvider _clientServiceProvider;
        private readonly ICommunicator _communicator;
        private readonly ILogger<CommandRoutingService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _defaultCommandPrefix;
        private readonly IEnumerable<ulong> _ownerIDs;
        private readonly IFrameworkGuildConfigProvider _guildConfigProvider;
        private readonly ICommandParser _commandParser;

        private readonly Dictionary<string, List<CommandInfo>> _commandsMapping = new Dictionary<string, List<CommandInfo>>();
        private readonly object _commandsMappingLock = new object();
        private int _commandCounter = 0;

        public CommandRoutingService(
            FrameworkConfiguration config,
            BaseSocketClient client, 
            ICommunicator communicator,
            IFrameworkGuildConfigProvider guildConfigProvider,
            ICommandParser commandParser,
            ILogger<CommandRoutingService> logger,
            ILoggerFactory loggerFactory)
        {
            _client = client;
            _clientServiceProvider = config.ClientServiceProvider;
            _communicator = communicator;
            _defaultCommandPrefix = config.DefaultCommandPrefix;
            _ownerIDs = config.OwnerIDs;
            _guildConfigProvider = guildConfigProvider;
            _commandParser = commandParser;
            _logger = logger;
            _loggerFactory = loggerFactory;

            foreach (var module in config.Modules)
                AddModule(module);
        }

        public Task StartAsync(CancellationToken ct)
        {
            _client.MessageReceived += HandleMessageReceived;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _client.MessageReceived -= HandleMessageReceived;
        }

        private Task HandleMessageReceived(SocketMessage message)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    // Filter messages from bots
                    if (message.Author.IsBot)
                        return;

                    // Filter anything but normal user messages
                    if (!(message is SocketUserMessage userMessage))
                        return;

                    // Try to find a command registration for this message
                    var findResult = await FindCommandRegistrationAsync(userMessage);
                    if (findResult == null)
                        return;

                    await HandleCommand(userMessage, findResult);
                }
                catch (Exception ex)
                {
                    _logger.WithScope(message).LogError(ex, "Failed to process potential command message.");
                }
            });

            return Task.CompletedTask;
        }

        private async Task HandleCommand(SocketUserMessage message, CommandRegistrationFindResult findResult)
        {
            var correlationId = Guid.NewGuid();
            var logger = _logger.WithCommandScope(message, correlationId, findResult.Registration, findResult.Usage);
            
            var stopwatch = Stopwatch.StartNew();
            var gatewayDelay = DateTimeOffset.UtcNow - message.Timestamp;

            var counter = Interlocked.Increment(ref _commandCounter);
            var guild = (message.Channel as IGuildChannel)?.Guild;

            // Don't log command content for non-guild channels, since these commands are usually meant to be private
            if (guild != null)
                logger.LogInformation("Command {CommandCounter} {MessageContent} with {MessageAttachmentCount} attachments", counter, message.Content, message.Attachments.Count);
            else
                logger.LogInformation("Command {CommandCounter} {MessageContentRedacted} with {MessageAttachmentCount} attachments", counter, findResult.Prefix + findResult.Usage.InvokeUsage, message.Attachments.Count);

            // Check if the channel type is valid for this command
            if (!IsValidCommandSource(message.Channel, findResult.Registration))
            {
                logger.LogInformation("Command {CommandCounter} rejected in {TotalElapsed:F3}s (g: {GatewayElapsed:F3}s) due to invalid channel type", counter, stopwatch.Elapsed.TotalSeconds, gatewayDelay.TotalSeconds);

                if (message.Channel is ITextChannel && findResult.Registration.Flags.HasFlag(CommandFlags.DirectMessageOnly))
                    await _communicator.CommandReplyDirectMessageOnly(message.Channel, findResult.Registration);

                return;
            }

            // Check owner
            if (findResult.Registration.Flags.HasFlag(CommandFlags.OwnerOnly) && !_ownerIDs.Contains(message.Author.Id))
            {
                logger.LogInformation("Command {CommandCounter} rejected in {TotalElapsed:F3}s (g: {GatewayElapsed:F3}s) due to the user not being an owner", counter, stopwatch.Elapsed.TotalSeconds, gatewayDelay.TotalSeconds);
                await _communicator.CommandReplyNotOwner(message.Channel, findResult.Registration);
                return;
            }

            // Check guild permisssions
            if (message.Channel is IGuildChannel guildChannel)
            {
                // User
                var guildUser = message.Author as IGuildUser;
                var missingPermissions = findResult.Registration.UserPermissions.Where(x => !guildUser.GuildPermissions.Has(x));
                if (missingPermissions.Any())
                {
                    logger.LogInformation("Command {CommandCounter} rejected in {TotalElapsed:F3}s (g: {GatewayElapsed:F3}s) due to missing user permissions", counter, stopwatch.Elapsed.TotalSeconds, gatewayDelay.TotalSeconds);
                    await _communicator.CommandReplyMissingPermissions(message.Channel, findResult.Registration, missingPermissions);
                    return;
                }

                // Bot
                var selfUser = await guild.GetCurrentUserAsync();
                var missingBotPermissions = findResult.Registration.BotPermissions.Where(x => !selfUser.GuildPermissions.Has(x));
                if (missingBotPermissions.Any())
                {
                    logger.LogInformation("Command {CommandCounter} rejected in {TotalElapsed:F3}s (g: {GatewayElapsed:F3}s) due to missing bot permissions", counter, stopwatch.Elapsed.TotalSeconds, gatewayDelay.TotalSeconds);
                    await _communicator.CommandReplyMissingBotPermissions(message.Channel, findResult.Registration, missingBotPermissions);
                    return;
                }
            }

            var verificationElapsed = stopwatch.Elapsed;

            // Create command
            var parseResult = await _commandParser.Parse(message, findResult.Registration, findResult.Usage, findResult.Prefix);
            if (parseResult.Type != CommandParseResultType.Success)
            {
                string explanation = "";
                switch (parseResult.Type)
                {
                    case CommandParseResultType.NotEnoughParameters: explanation = Properties.Resources.Command_NotEnoughParameters; break;
                    case CommandParseResultType.TooManyParameters: explanation = Properties.Resources.Command_TooManyParameters; break;
                    case CommandParseResultType.InvalidParameterFormat: explanation = string.Format(Properties.Resources.Command_InvalidParameterFormat, ((InvalidParameterCommandParseResult)parseResult).InvalidPosition); break;
                }

                logger.LogInformation("Command {CommandCounter} rejected in {TotalElapsed:F3}s (g: {GatewayElapsed:F3}s) due to incorrect parameters", counter, stopwatch.Elapsed.TotalSeconds, gatewayDelay.TotalSeconds);
                await _communicator.CommandReplyIncorrectParameters(message.Channel, findResult.Registration, explanation, findResult.Prefix);
                return;
            }

            var command = new Command((SuccessCommandParseResult)parseResult, _communicator);
            var parsingElapsed = stopwatch.Elapsed;

            // Execute
            if (findResult.Registration.Flags.HasFlag(CommandFlags.Synchronous))
            {
                await ExecuteCommandAsync(counter, correlationId, logger, findResult, command, stopwatch, verificationElapsed, parsingElapsed, gatewayDelay);
            }
            else
            {
                TaskHelper.FireForget(() => ExecuteCommandAsync(counter, correlationId, logger, findResult, command, stopwatch, verificationElapsed, parsingElapsed, gatewayDelay),
                    x => logger.LogError(x, "Uncaught exception while handling command."));
            }
        }

        private void AddModule(ModuleInfo module)
        {
            foreach (var command in module.Commands)
            {
                foreach (var usage in command.EveryUsage)
                {
                    if (!_commandsMapping.TryGetValue(usage.InvokeString.ToLowerInvariant(), out var commands))
                        _commandsMapping.Add(usage.InvokeString.ToLowerInvariant(), commands = new List<CommandInfo>());

                    commands.Add(command);
                }
            }
        }

        private async Task<CommandRegistrationFindResult> FindCommandRegistrationAsync(SocketUserMessage userMessage)
        {
            var prefix = _defaultCommandPrefix;
            if (userMessage.Channel is ITextChannel guildChannel)
            {
                var guildConfig = await _guildConfigProvider.GetConfigAsync(guildChannel.GuildId);
                if (!string.IsNullOrEmpty(guildConfig?.CustomCommandPrefix))
                    prefix = guildConfig.CustomCommandPrefix;
            }

            // Check prefix
            if (!userMessage.Content.StartsWith(prefix))
                return null;

            // Check if the message contains a command invoker
            var invoker = _commandParser.ParseInvoker(userMessage.Content, prefix);
            if (string.IsNullOrEmpty(invoker))
                return null;

            lock (_commandsMappingLock)
            {
                // Try to find command registrations for this invoker
                List<CommandInfo> commandRegistrations;
                if (!_commandsMapping.TryGetValue(invoker.ToLowerInvariant(), out commandRegistrations))
                    return null;

                var match = _commandParser.Match(userMessage.Content, prefix, commandRegistrations);
                if (match == null)
                    return null;

                var (registration, usage) = match.Value;
                return new CommandRegistrationFindResult(prefix, registration, usage);
            }
        }

        private bool IsValidCommandSource(IMessageChannel channel, CommandInfo cr)
        {
            if (channel is ITextChannel && !cr.Flags.HasFlag(CommandFlags.DirectMessageOnly))
                return true;

            if (channel is IDMChannel && (cr.Flags.HasFlag(CommandFlags.DirectMessageAllow) || cr.Flags.HasFlag(CommandFlags.DirectMessageOnly)))
                return true;

            return false;
        }

        private async Task ExecuteCommandAsync(int counter, Guid correlationId, ILogger logger, CommandRegistrationFindResult findResult, ICommand command, Stopwatch stopwatch, TimeSpan verificationElapsed, TimeSpan parsingElapsed, TimeSpan gatewayPing)
        {
            IDisposable typingState = null;
            if (findResult.Registration.Flags.HasFlag(CommandFlags.TypingIndicator))
                typingState = command.Message.Channel.EnterTypingState();

            var result = CommandResult.Failed;
            try
            {
                using (var scope = _clientServiceProvider.CreateScope())
                {
                    var module = scope.ServiceProvider.GetRequiredService(findResult.Registration.ModuleType);
                    var commandLogger = _loggerFactory.CreateLogger(findResult.Registration.ModuleType)
                        .WithCommandScope(command.Message, correlationId, findResult.Registration, findResult.Usage);

                    await findResult.Registration.Handler.Invoke(module, command, commandLogger, default); // TODO: cancellation
                }

                result = CommandResult.Succeeded;
            }
            catch (Exceptions.AbortException ex)
            {
                if (ex.Pages != null)
                    await _communicator.CommandReply(command.Message.Channel, ex.Pages);
                else if (!string.IsNullOrEmpty(ex.Message))
                    await _communicator.CommandReply(command.Message.Channel, ex.Message);

                result = CommandResult.Succeeded;
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
                logger.LogError(ex, "Exception encountered while processing command {Command} (nr: {CommandCounter}) in module {Module}", findResult.Registration.PrimaryUsage.InvokeUsage, counter, findResult.Registration.ModuleType);
                await _communicator.CommandReplyGenericFailure(command.Message.Channel);
            }
            finally
            {
                if (typingState != null)
                    typingState.Dispose();
            }

            var totalElapsed = stopwatch.Elapsed;
            logger.LogInformation("Command {CommandCounter} {Result} in {TotalElapsed:F3}s (v: {VerificationElapsed:F3}s, p: {ParsingElapsed:F3}s, e: {ExecutionElapsed:F3}s, g: {GatewayDelay:F3}s)", counter, result, totalElapsed.TotalSeconds, verificationElapsed.TotalSeconds, (parsingElapsed - verificationElapsed).TotalSeconds, (totalElapsed - parsingElapsed).TotalSeconds, gatewayPing.TotalSeconds);
        }
    }
}
