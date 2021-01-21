using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DustyBot.Configuration;
using DustyBot.Core.Async;
using DustyBot.Core.Collections;
using DustyBot.Core.Formatting;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;
using DustyBot.Framework.Utility;
using DustyBot.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DustyBot.Modules
{
    [Module("Raid protection", "Protect your server against raiders.")]
    internal sealed class RaidProtectionModule : IDisposable
    {
        private class SlidingMessageCache : ICollection<IMessage>
        {
            private List<IMessage> _data = new List<IMessage>();

            public int Count => _data.Count;
            public bool IsReadOnly => false;

            public void Add(IMessage item)
            {
                var i = _data.FindLastIndex(x => x.Timestamp <= item.Timestamp);
                _data.Insert(i < 0 ? 0 : i + 1, item);
            }

            public void SlideWindow(TimeSpan windowSize)
            {
                var last = _data.LastOrDefault();
                if (last != null)
                    _data.RemoveAll(x => x.Timestamp < last.Timestamp - windowSize);
            }

            public void SlideWindow(TimeSpan windowSize, DateTimeOffset end)
            {
                _data.RemoveAll(x => x.Timestamp < end - windowSize);
            }

            public void Clear() => _data.Clear();
            public bool Contains(IMessage item) => _data.Contains(item);
            public void CopyTo(IMessage[] array, int arrayIndex) => _data.CopyTo(array, arrayIndex);
            public IEnumerator<IMessage> GetEnumerator() => _data.GetEnumerator();
            public bool Remove(IMessage item) => _data.Remove(item);
            IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();
        }

        private abstract class BaseContext
        {
            public SemaphoreSlim Mutex { get; } = new SemaphoreSlim(1, 1);
        }

        private class GlobalContext : BaseContext
        {
            public DateTime LastCleanup { get; set; } = DateTime.MinValue;
            public DateTime LastReport { get; set; } = DateTime.MinValue;
            public Dictionary<ulong, GuildContext> GuildContexts { get; } = new Dictionary<ulong, GuildContext>();
        }

        private class GuildContext : BaseContext
        {
            public Dictionary<ulong, UserContext> UserContexts { get; } = new Dictionary<ulong, UserContext>();
        }

        private class UserContext : BaseContext
        {
            public Dictionary<RaidProtectionRuleType, SlidingMessageCache> Offenses { get; } = new Dictionary<RaidProtectionRuleType, SlidingMessageCache>();
            public SlidingMessageCache ImagePosts { get; } = new SlidingMessageCache();
            public SlidingMessageCache TextPosts { get; } = new SlidingMessageCache();

            public bool Empty => Offenses.Count <= 0 && ImagePosts.Count <= 0 && TextPosts.Count <= 0;
        }

        private static readonly TimeSpan MaxMessageProcessingDelay = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan CleanupTimer = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan ReportTimer = TimeSpan.FromMinutes(30);

        private readonly BaseSocketClient _client;
        private readonly ICommunicator _communicator;
        private readonly ISettingsService _settings;
        private readonly ILogger<RaidProtectionModule> _logger;
        private readonly DiscordRestClient _restClient;
        private readonly IFrameworkReflector _frameworkReflector;
        private readonly HelpBuilder _helpBuilder;
        private readonly IOptions<WebOptions> _webOptions;

        private readonly GlobalContext _context = new GlobalContext();

        public RaidProtectionModule(
            BaseSocketClient client, 
            ICommunicator communicator, 
            ISettingsService settings, 
            ILogger<RaidProtectionModule> logger, 
            DiscordRestClient restClient, 
            IFrameworkReflector frameworkReflector,
            HelpBuilder helpBuilder,
            IOptions<WebOptions> webOptions)
        {
            _client = client;
            _communicator = communicator;
            _settings = settings;
            _logger = logger;
            _restClient = restClient;
            _frameworkReflector = frameworkReflector;
            _helpBuilder = helpBuilder;
            _webOptions = webOptions;

            _client.MessageReceived += HandleMessageReceived;
        }

        [Command("raid", "protection", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("raid", "protection"), Alias("raid-protection"), Alias("raid-protection", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(_helpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
        }

        [Command("raid", "protection", "enable", "Protects the server against raids.")]
        [Alias("raid-protection", "enable")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles, GuildPermission.ManageMessages)]
        [Parameter("LogChannel", ParameterType.TextChannel, ParameterFlags.Remainder, "a channel that will recieve notifications about performed actions")]
        [Comment("Upon enabling this feature, the bot will automatically delete obviously malicious messages and warn or mute offending users. The default rules are set up to only affect obvious raiders.")]
        public async Task EnableRaidProtection(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["LogChannel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError($"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            await _settings.Modify(command.GuildId, (RaidProtectionSettings x) =>
            {
                x.Enabled = true;
                x.LogChannel = command["LogChannel"].AsTextChannel.Id;
            });

            await command.ReplySuccess($"Raid protection has been enabled. Use `raid protection rules` to see the active rules.");
        }

        [Command("raid", "protection", "disable", "Disables raid protection.")]
        [Alias("raid-protection", "disable")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Comment("Does not erase your current rules.")]
        public async Task DisableRaidProtection(ICommand command)
        {
            await _settings.Modify(command.GuildId, (RaidProtectionSettings x) => x.Enabled = false);
            await command.ReplySuccess($"Raid protection has been disabled.");
        }

        [Command("raid", "protection", "rules", "Displays active raid protection rules.")]
        [Alias("raid-protection", "rules")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        public async Task ListRulesRaidProtection(ICommand command)
        {
            var settings = await _settings.Read<RaidProtectionSettings>(command.GuildId);
            var result = new StringBuilder();
            result.AppendLine($"Protection **{(settings.Enabled ? "enabled" : "disabled")}**.");
            result.AppendLine($"Please make a request in the support server to modify any of these settings for your server (<{_webOptions.Value.SupportServerInvite}>).");
            result.AppendLine("Phrase blacklist can be set with the `raid protection blacklist add` command.\n");

            var printDefaultFlag = new Func<RaidProtectionRuleType, string>(x => settings.IsDefault(x) ? " (default)" : "");
            result.AppendLine($"**MassMentionsRule** - if enabled, blocks messages containing more than {settings.MassMentionsRule.MentionsLimit} mentioned users" + printDefaultFlag(RaidProtectionRuleType.MassMentionsRule));
            result.AppendLine("`" + settings.MassMentionsRule + "`\n");

            result.AppendLine($"**TextSpamRule** - if enabled, blocks more than {settings.TextSpamRule.Threshold} messages sent in {settings.TextSpamRule.Window.TotalSeconds} seconds by one user" + printDefaultFlag(RaidProtectionRuleType.TextSpamRule));
            result.AppendLine("`" + settings.TextSpamRule + "`\n");

            result.AppendLine($"**ImageSpamRule** - if enabled, blocks more than {settings.ImageSpamRule.Threshold} images sent in {settings.ImageSpamRule.Window.TotalSeconds} seconds by one user" + printDefaultFlag(RaidProtectionRuleType.ImageSpamRule));
            result.AppendLine("`" + settings.ImageSpamRule + "`\n");

            result.AppendLine($"**PhraseBlacklistRule** - if enabled, blocks messages containing any of the specified phrases" + printDefaultFlag(RaidProtectionRuleType.PhraseBlacklistRule));
            result.AppendLine("`" + settings.PhraseBlacklistRule + "`\n");

            await command.Reply(result.ToString());
        }

        [Command("raid", "protection", "blacklist", "add", "Adds one or more blacklisted phrases.")]
        [Alias("raid-protection", "blacklist", "add", true)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Phrases", ParameterType.String, ParameterFlags.Repeatable, "one or more phrases separated by spaces")]
        [Comment("Use the `*` wildcard before or after the phrase to also match longer phrases. For example, `darn*` also matches `darnit`. Matching is not case sensitive.\n\n Messages that match any of these phrases will be handled according to the PhraseBlacklist rule (default: the offending message will be deleted and upon commiting 3 offenses within 5 minutes the offending user will be muted).")]
        [Example("darn*")]
        [Example("\"fudge nugget\" *dang")]
        public async Task AddBlacklistRaidProtection(ICommand command)
        {
            const int minLength = 3;
            const int guildLimit = 50;

            var phrases = command["Phrases"].Repeats.Select(x => x.AsString).ToList();
            var tooShort = phrases.Where(x => x.Length < minLength);
            if (tooShort.Any())
                throw new IncorrectParametersCommandException($"A phrase needs to be at least {minLength} characters long. The following phrases are too short: {tooShort.WordJoinQuoted()}.", false);

            var added = await _settings.Modify(command.GuildId, (RaidProtectionSettings x) =>
            {
                if (x.PhraseBlacklistRule.Blacklist.Count + phrases.Count > guildLimit)
                    throw new AbortException($"You can only have up to {guildLimit} blacklisted phrases in a server.");

                x.SetException(RaidProtectionRuleType.PhraseBlacklistRule, new PhraseBlacklistRule()
                {
                    Type = x.PhraseBlacklistRule.Type,
                    Enabled = true,
                    Delete = x.PhraseBlacklistRule.Delete,
                    MaxOffenseCount = x.PhraseBlacklistRule.MaxOffenseCount,
                    OffenseWindow = x.PhraseBlacklistRule.OffenseWindow,
                    Blacklist = x.PhraseBlacklistRule.Blacklist.Concat(phrases).Distinct().ToList()
                });

                return phrases;
            });

            await command.ReplySuccess($"The following phrases have been added to the blacklist: {added.WordJoinQuoted()}.");
        }

        [Command("raid", "protection", "blacklist", "remove", "Removes one or more blacklisted phrases.")]
        [Alias("raid-protection", "blacklist", "remove", true)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Phrases", ParameterType.String, ParameterFlags.Repeatable, "one or more phrases separated by spaces")]
        [Example("darn*")]
        [Example("\"fudge nugget\" *dang")]
        public async Task RemoveBlacklistRaidProtection(ICommand command)
        {
            var phrases = command["Phrases"].Repeats.Select(x => x.AsString).ToList();
            await _settings.Modify(command.GuildId, (RaidProtectionSettings x) =>
            {
                var missing = phrases.Except(x.PhraseBlacklistRule.Blacklist);
                if (missing.Any())
                    throw new IncorrectParametersCommandException($"Phrases {missing.WordJoinQuoted()} are not in the blacklist.", false);

                var complement = x.PhraseBlacklistRule.Blacklist.Except(phrases).ToList();
                x.SetException(RaidProtectionRuleType.PhraseBlacklistRule, new PhraseBlacklistRule()
                {
                    Type = x.PhraseBlacklistRule.Type,
                    Enabled = complement.Any(),
                    Delete = x.PhraseBlacklistRule.Delete,
                    MaxOffenseCount = x.PhraseBlacklistRule.MaxOffenseCount,
                    OffenseWindow = x.PhraseBlacklistRule.OffenseWindow,
                    Blacklist = complement
                });
            });

            await command.ReplySuccess($"The following phrases have been removed from the blacklist: {phrases.WordJoinQuoted()}.");
        }

        [Command("raid", "protection", "blacklist", "clear", "Removes all phrases from the blacklist.")]
        [Alias("raid-protection", "blacklist", "clear", true)]
        [Permissions(GuildPermission.Administrator)]
        public async Task ClearBlacklistRaidProtection(ICommand command)
        {
            await _settings.Modify(command.GuildId, (RaidProtectionSettings x) =>
            {
                x.SetException(RaidProtectionRuleType.PhraseBlacklistRule, new PhraseBlacklistRule()
                {
                    Type = x.PhraseBlacklistRule.Type,
                    Enabled = false,
                    Delete = x.PhraseBlacklistRule.Delete,
                    MaxOffenseCount = x.PhraseBlacklistRule.MaxOffenseCount,
                    OffenseWindow = x.PhraseBlacklistRule.OffenseWindow,
                    Blacklist = new List<string>()
                });
            });

            await command.ReplySuccess($"The phrase blacklist has been disabled.");
        }

        [Command("raid", "protection", "rules", "set", "Modifies a raid protection rule.", CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Parameter("ServerId", ParameterType.Id)]
        [Parameter("Type", ParameterType.String)]
        [Parameter("Rule", ParameterType.String, ParameterFlags.Remainder)]
        public async Task SetRulesRaidProtection(ICommand command)
        {
            if (!Enum.TryParse<RaidProtectionRuleType>(command["Type"], out var type))
                throw new IncorrectParametersCommandException("Unknown rule type.");

            RaidProtectionRule newRule;
            try
            {
                newRule = RaidProtectionRule.Create(type, command["Rule"]);
            }
            catch (Exception)
            {
                throw new IncorrectParametersCommandException("Invalid rule.");
            }

            await _settings.Modify((ulong)command["ServerId"], (RaidProtectionSettings x) => x.SetException(type, newRule));
            await command.ReplySuccess($"Rule has been set.");
        }

        [Command("raid", "protection", "rules", "reset", "Resets a raid protection rule to default.", CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Parameter("ServerId", ParameterType.Id)]
        [Parameter("Type", ParameterType.String)]
        public async Task ResetRulesRaidProtection(ICommand command)
        {
            if (!Enum.TryParse<RaidProtectionRuleType>(command["Type"], out var type))
                throw new IncorrectParametersCommandException("Unknown rule type.");

            await _settings.Modify((ulong)command["ServerId"], (RaidProtectionSettings x) => x.ResetException(type));
            await command.ReplySuccess($"Rule has been reset.");
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
                    var channel = message.Channel as ITextChannel;
                    if (channel == null)
                        return;

                    var user = message.Author as IGuildUser;
                    if (user == null)
                        return;

                    if (user.IsBot)
                        return;

                    var settings = await _settings.Read<RaidProtectionSettings>(channel.GuildId, false);
                    if (settings == null || !settings.Enabled)
                        return;

                    if (user.GuildPermissions.Administrator)
                        return;

                    if (settings.MassMentionsRule.Enabled)
                    {
                        if (message.MentionedUsers.Where(x => !x.IsBot).Select(x => x.Id).Distinct().Count() >= settings.MassMentionsRule.MentionsLimit)
                        {
                            await EnforceRule(settings.MassMentionsRule, new IMessage[] { message }, channel, await GetUserContext(user), user, settings.LogChannel, message.Content);
                            return;
                        }
                    }

                    if (settings.PhraseBlacklistRule.Enabled)
                    {
                        if (MatchBlacklist(message.Content, settings.PhraseBlacklistRule.Blacklist))
                        {
                            await EnforceRule(settings.PhraseBlacklistRule, new IMessage[] { message }, channel, await GetUserContext(user), user, settings.LogChannel, message.Content);
                            return;
                        }
                    }

                    if (settings.ImageSpamRule.Enabled || settings.TextSpamRule.Enabled)
                    {
                        var userContext = await GetUserContext(user);
                        bool enforce = false;
                        var offendingMessages = new List<IMessage>();
                        ReadOnlySpamRule appliedRule;
                        try
                        {
                            await userContext.Mutex.WaitAsync();

                            if ((string.IsNullOrEmpty(message.Content) && message.Attachments.Count > 0) ||
                                (!string.IsNullOrEmpty(message.Content) && Uri.IsWellFormedUriString(message.Content.Trim(), UriKind.Absolute)))
                            {
                                // Image post
                                appliedRule = settings.ImageSpamRule;
                                if (settings.ImageSpamRule.Enabled)
                                {
                                    userContext.ImagePosts.Add(message);
                                    userContext.ImagePosts.SlideWindow(settings.ImageSpamRule.Window);
                                    if (userContext.ImagePosts.Count >= settings.ImageSpamRule.Threshold)
                                    {
                                        enforce = true;
                                        offendingMessages = userContext.ImagePosts.ToList();
                                        userContext.ImagePosts.Clear();
                                    }
                                }
                            }
                            else
                            {
                                // Text post
                                appliedRule = settings.TextSpamRule;
                                if (settings.TextSpamRule.Enabled)
                                {
                                    userContext.TextPosts.Add(message);
                                    userContext.TextPosts.SlideWindow(settings.TextSpamRule.Window);
                                    if (userContext.TextPosts.Count >= settings.TextSpamRule.Threshold)
                                    {
                                        enforce = true;
                                        offendingMessages = userContext.TextPosts.ToList();
                                        userContext.TextPosts.Clear();
                                    }
                                }
                            }
                        }
                        finally
                        {
                            userContext.Mutex.Release();
                        }

                        if (enforce)
                        {
                            await EnforceRule(appliedRule, offendingMessages, channel, await GetUserContext(user), user, settings.LogChannel, $"Sent {offendingMessages.Count} messages in under {appliedRule.Window.TotalSeconds} seconds.");
                            return;
                        }
                    }

                    // Cleanup
                    if (_context.LastCleanup < DateTime.UtcNow - CleanupTimer)
                        await CleanupContext(settings);
                }
                catch (Exception ex)
                {
                    _logger.WithScope(message).LogError(ex, "Failed to process message for raid protection");
                }
            });

            return Task.CompletedTask;
        }

        private bool MatchBlacklist(string message, IEnumerable<string> blacklist)
        {
            // TODO: inefficient
            return blacklist.Any(x =>
            {
                var phrase = x.Trim('*', 1);
                var i = message.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
                if (i == -1)
                    return false;

                if (i > 0 && char.IsLetter(message[i - 1]) && x.First() != '*')
                    return false; // Inside a word (prefixed) - skip

                if (i + phrase.Length < message.Length && char.IsLetter(message[i + phrase.Length]) && x.Last() != '*')
                    return false; // Inside a word (suffixed) - skip

                return true;
            });
        }

        private async Task CleanupContext(RaidProtectionSettings settings)
        {
            try
            {
                // Guild contexts
                await _context.Mutex.WaitAsync();

                DateTime now = DateTime.UtcNow;
                if (_context.LastCleanup > now - CleanupTimer)
                    return;

                _context.LastCleanup = now;

                var toRemove = new List<ulong>();
                foreach (var guildContext in _context.GuildContexts)
                {
                    try
                    {
                        // User contexts
                        await guildContext.Value.Mutex.WaitAsync();
                        foreach (var userContext in guildContext.Value.UserContexts.ToList())
                        {
                            try
                            {
                                await userContext.Value.Mutex.WaitAsync();
                                foreach (var offenses in userContext.Value.Offenses.ToList())
                                {
                                    offenses.Value.SlideWindow(settings.GetRule<ReadOnlyRaidProtectionRule>(offenses.Key).OffenseWindow, now - MaxMessageProcessingDelay);
                                    if (offenses.Value.Count <= 0)
                                        userContext.Value.Offenses.Remove(offenses.Key);
                                }

                                userContext.Value.TextPosts.SlideWindow(settings.TextSpamRule.Window, now - MaxMessageProcessingDelay);
                                userContext.Value.ImagePosts.SlideWindow(settings.ImageSpamRule.Window, now - MaxMessageProcessingDelay);

                                if (userContext.Value.Empty)
                                    guildContext.Value.UserContexts.Remove(userContext.Key);
                            }
                            finally
                            {
                                userContext.Value.Mutex.Release();
                            }
                        }

                        if (guildContext.Value.UserContexts.Count <= 0)
                            toRemove.Add(guildContext.Key);
                    }
                    finally
                    {
                        guildContext.Value.Mutex.Release();
                    }
                }

                foreach (var removable in toRemove)
                    _context.GuildContexts.Remove(removable);

                if (_context.LastReport < now - ReportTimer)
                {
                    _context.LastReport = now;
                    _logger.LogInformation("Raid protection status: {RaidProtectionGuildContextCount} guild contexts, {RaidProtectionUserContextCount} user contexts", 
                        _context.GuildContexts.Count, 
                        _context.GuildContexts.Aggregate(0, (x, y) => x + y.Value.UserContexts.Count));
                }
            }
            finally
            {
                _context.Mutex.Release();
            }
        }

        private async Task<UserContext> GetUserContext(IGuildUser user)
        {
            GuildContext guildContext;
            try
            {
                await _context.Mutex.WaitAsync();
                guildContext = _context.GuildContexts.GetOrCreate(user.GuildId);
            }
            finally
            {
                _context.Mutex.Release();
            }

            UserContext userContext;
            try
            {
                await guildContext.Mutex.WaitAsync();
                userContext = guildContext.UserContexts.GetOrCreate(user.Id);
            }
            finally
            {
                guildContext.Mutex.Release();
            }

            return userContext;
        }

        private async Task EnforceRule(ReadOnlyRaidProtectionRule rule, ICollection<IMessage> messages, ITextChannel channel, UserContext userContext, IGuildUser user, ulong logChannelId, string reason)
        {
            if (rule.Delete)
            {
                foreach (var message in messages)
                    TaskHelper.FireForget(async () => await message.DeleteAsync());
            }

            if (rule.MaxOffenseCount > 0 && rule.OffenseWindow > TimeSpan.FromSeconds(0))
            {
                bool punish = false;
                try
                {
                    await userContext.Mutex.WaitAsync();

                    var offenses = userContext.Offenses.GetOrCreate(rule.Type);
                    offenses.Add(messages.Last());
                    offenses.SlideWindow(rule.OffenseWindow);

                    if (offenses.Count >= rule.MaxOffenseCount)
                    {
                        offenses.Clear();
                        punish = true;
                    }
                }
                finally
                {
                    userContext.Mutex.Release();
                }

                var logger = _logger.WithScope(user);
                var currentUser = await channel.Guild.GetCurrentUserAsync();
                if (currentUser.GetPermissions(channel).SendMessages)
                {
                    IUserMessage warningMessage;
                    if (punish)
                    {
                        await AdministrationHelpers.Mute(user, "raid protection rule", _settings);
                        warningMessage = (await _communicator.SendMessage(channel, $"{user.Mention} you have been muted for breaking raid protection rules. If you believe this is a mistake, please contact a moderator.")).First();
                    }
                    else
                    {
                        warningMessage = (await _communicator.SendMessage(channel, $"{user.Mention} you have broken a raid protection rule.")).First();
                    }

                    warningMessage.DeleteAfter(8);
                }
                else
                {
                    logger.LogInformation("Missing permissions to warn offender about rule {RaidProtectionRuleType}, punished: {Punished}", rule.Type, punish);
                }

                var logChannel = await channel.Guild.GetTextChannelAsync(logChannelId);
                if (logChannel != null && currentUser.GetPermissions(logChannel).SendMessages)
                {
                    var embed = new EmbedBuilder()
                        .WithFooter(fb => fb.WithText(messages.Last().Timestamp.ToUniversalTime().ToString("dd.MM.yyyy H:mm:ss UTC")))
                        .AddField(fb => fb.WithName("Reason").WithValue($"Broken rule ({rule.Type})."));

                    if (punish)
                        embed.WithDescription($"**Muted user {user.Mention} for suspicious behavior:**\n" + reason).WithColor(Color.Red);
                    else
                        embed.WithDescription($"**Warned user {user.Mention} for suspicious behavior:**\n" + reason).WithColor(Color.Orange);

                    await _communicator.SendMessage(logChannel, embed.Build());
                }
                else
                {
                    logger.LogInformation("Couldn't report about rule {RaidProtectionRuleType}, punished: {Punished}", rule.Type, punish);
                }

                logger.LogInformation("Enforced rule {RaidProtectionRuleType}, punished: {Punished}, reason: {RaidProtectionReason}", rule.Type, punish, reason);
            }
        }
    }
}
