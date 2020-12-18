using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using System.Diagnostics;
using DustyBot.Core.Async;
using DustyBot.Framework.Utility;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands.Parsing;

namespace DustyBot.Framework.Commands
{
    internal sealed class CommandRoutingService : IDisposable
    {
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
        private readonly string _defaultCommandPrefix;
        private readonly IEnumerable<ulong> _ownerIDs;
        private readonly IFrameworkGuildConfigProvider _guildConfigProvider;
        private readonly ICommandParser _commandParser;
        private readonly IUserFetcher _userFetcher;

        private readonly Dictionary<string, List<CommandInfo>> _commandsMapping = new Dictionary<string, List<CommandInfo>>();
        private readonly object _commandsMappingLock = new object();
        private int _commandCounter = 0;

        public CommandRoutingService(FrameworkConfiguration config, ICommandParser commandParser, IUserFetcher userFetcher)
        {
            _client = config.DiscordClient;
            _clientServiceProvider = config.ClientServiceProvider;
            _communicator = config.Communicator;
            _logger = config.ClientServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<CommandRoutingService>();
            _defaultCommandPrefix = config.DefaultCommandPrefix;
            _ownerIDs = config.OwnerIDs;
            _guildConfigProvider = config.GuildConfigProvider;
            _commandParser = commandParser;
            _userFetcher = userFetcher;

            foreach (var module in config.Modules)
                AddModule(module);

            _client.MessageReceived += HandleMessageReceived;
        }

        public async Task HandleMessageReceived(SocketMessage message)
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

                var stopwatch = Stopwatch.StartNew();
                var gatewayPing = DateTimeOffset.UtcNow - message.Timestamp;

                // Log; don't log command content for non-guild channels, since these commands are usually meant to be private
                var id = ++_commandCounter;
                var guild = (userMessage.Channel as IGuildChannel)?.Guild;
                if (guild != null)
                    _logger.LogInformation("\"{MessageContent}\" {MessageAttachmentCount} attachments (id: {CommandId}) by {AuthorUsername} ({AuthorId}) in #{ChannelName} on {GuildName} ({GuildId})", message.Content, message.Attachments.Count, id, message.Author.Username, message.Author.Id, message.Channel.Name, guild.Name, guild.Id);
                else
                    _logger.LogInformation("\"{Command}\" {MessageAttachmentCount} attachments (id: {CommandId}) by {AuthorUsername} ({AuthorId})", findResult.Prefix + findResult.Usage.InvokeUsage, message.Attachments.Count, id, message.Author.Username, message.Author.Id);

                // Check if the channel type is valid for this command
                if (!IsValidCommandSource(message.Channel, findResult.Registration))
                {
                    _logger.LogInformation("Command {CommandId} rejected in {TotalElapsed:F3}s (g: {GatewayElapsed:F3}s) due to invalid channel type", id, stopwatch.Elapsed.TotalSeconds, gatewayPing.TotalSeconds);

                    if (message.Channel is ITextChannel && findResult.Registration.Flags.HasFlag(CommandFlags.DirectMessageOnly))
                        await _communicator.CommandReplyDirectMessageOnly(message.Channel, findResult.Registration);

                    return;
                }

                // Check owner
                if (findResult.Registration.Flags.HasFlag(CommandFlags.OwnerOnly) && !_ownerIDs.Contains(message.Author.Id))
                {
                    _logger.LogInformation("Command {CommandId} rejected in {TotalElapsed:F3}s (g: {GatewayElapsed:F3}s) due to the user not being an owner", id, stopwatch.Elapsed.TotalSeconds, gatewayPing.TotalSeconds);
                    await _communicator.CommandReplyNotOwner(message.Channel, findResult.Registration);
                    return;
                }

                // Check guild permisssions
                if (userMessage.Channel is IGuildChannel guildChannel)
                {
                    // User
                    var guildUser = message.Author as IGuildUser;
                    var missingPermissions = findResult.Registration.UserPermissions.Where(x => !guildUser.GuildPermissions.Has(x));
                    if (missingPermissions.Any())
                    {
                        _logger.LogInformation("Command {CommandId} rejected in {TotalElapsed:F3}s (g: {GatewayElapsed:F3}s) due to missing user permissions", id, stopwatch.Elapsed.TotalSeconds, gatewayPing.TotalSeconds);
                        await _communicator.CommandReplyMissingPermissions(message.Channel, findResult.Registration, missingPermissions);
                        return;
                    }

                    // Bot
                    var selfUser = await guild.GetCurrentUserAsync();
                    var missingBotPermissions = findResult.Registration.BotPermissions.Where(x => !selfUser.GuildPermissions.Has(x));
                    if (missingBotPermissions.Any())
                    {
                        _logger.LogInformation("Command {CommandId} rejected in {TotalElapsed:F3}s (g: {GatewayElapsed:F3}s) due to missing bot permissions", id, stopwatch.Elapsed.TotalSeconds, gatewayPing.TotalSeconds);
                        await _communicator.CommandReplyMissingBotPermissions(message.Channel, findResult.Registration, missingBotPermissions);
                        return;
                    }
                }

                var verificationElapsed = stopwatch.Elapsed;

                // Create command
                var parseResult = await _commandParser.Parse(userMessage, findResult.Registration, findResult.Usage, findResult.Prefix);
                if (parseResult.Type != CommandParseResultType.Success)
                {
                    string explanation = "";
                    switch (parseResult.Type)
                    {
                        case CommandParseResultType.NotEnoughParameters: explanation = Properties.Resources.Command_NotEnoughParameters; break;
                        case CommandParseResultType.TooManyParameters: explanation = Properties.Resources.Command_TooManyParameters; break;
                        case CommandParseResultType.InvalidParameterFormat: explanation = string.Format(Properties.Resources.Command_InvalidParameterFormat, ((InvalidParameterCommandParseResult)parseResult).InvalidPosition); break;
                    }

                    _logger.LogInformation("Command {CommandId} rejected in {TotalElapsed:F3}s (g: {GatewayElapsed:F3}s) due to incorrect parameters", id, stopwatch.Elapsed.TotalSeconds, gatewayPing.TotalSeconds);
                    await _communicator.CommandReplyIncorrectParameters(message.Channel, findResult.Registration, explanation, findResult.Prefix);
                    return;
                }

                var command = new Command((SuccessCommandParseResult)parseResult, _communicator);
                var parsingElapsed = stopwatch.Elapsed;

                // Execute
                if (findResult.Registration.Flags.HasFlag(CommandFlags.Synchronous))
                {
                    await ExecuteCommandAsync(id, findResult, command, stopwatch, verificationElapsed, parsingElapsed, gatewayPing);
                }
                else
                {
                    TaskHelper.FireForget(() => ExecuteCommandAsync(id, findResult, command, stopwatch, verificationElapsed, parsingElapsed, gatewayPing),
                        x => _logger.LogError(x, "Uncaught exception while handling command."));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process potential command message.");
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

        private async Task ExecuteCommandAsync(int id, CommandRegistrationFindResult findResult, ICommand command, Stopwatch stopwatch, TimeSpan verificationElapsed, TimeSpan parsingElapsed, TimeSpan gatewayPing)
        {
            IDisposable typingState = null;
            if (findResult.Registration.Flags.HasFlag(CommandFlags.TypingIndicator))
                typingState = command.Message.Channel.EnterTypingState();

            bool succeeded = false;
            try
            {
                using (var scope = _clientServiceProvider.CreateScope())
                {
                    var module = scope.ServiceProvider.GetRequiredService(findResult.Registration.ModuleType);
                    await findResult.Registration.Handler(module, command);
                }

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
                _logger.LogError(ex, "Exception encountered while processing command {Command} (id: {CommandId}) in module {Module}", findResult.Registration.PrimaryUsage.InvokeUsage, id, findResult.Registration.ModuleType);
                await _communicator.CommandReplyGenericFailure(command.Message.Channel);
            }
            finally
            {
                if (typingState != null)
                    typingState.Dispose();
            }

            var totalElapsed = stopwatch.Elapsed;
            _logger.LogInformation("Command {CommandId} {Success} in {TotalElapsed:F3}s (v: {VerificationElapsed:F3}s, p: {ParsingElapsed:F3}s, e: {ExecutionElapsed:F3}s, g: {gatewayPing.TotalSeconds:F3}s)", id, succeeded, totalElapsed.TotalSeconds, verificationElapsed.TotalSeconds, parsingElapsed - verificationElapsed, totalElapsed - parsingElapsed, gatewayPing.TotalSeconds);
        }

        public void Dispose()
        {
            _client.MessageReceived -= HandleMessageReceived;
        }
    }
}
