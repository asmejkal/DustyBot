using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DustyBot.Core.Formatting;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using Discord.WebSocket;
using System.Threading;
using DustyBot.Framework.Logging;
using System.Collections;
using DustyBot.Helpers;
using Discord.Rest;
using DustyBot.Database.Services;
using DustyBot.Core.Async;
using DustyBot.Core.Collections;
using DustyBot.Framework.Exceptions;
using DustyBot.Definitions;
using System.Collections.Concurrent;
using NReco.Text;

namespace DustyBot.Modules
{
    [Module("Raid protection", "Protect your server against raiders.")]
    class RaidProtectionModule : Module
    {
        private class SlidingMessageCache : ICollection<IMessage>
        {
            List<IMessage> _data = new List<IMessage>();

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

        struct BlacklistKeywordProperties
        {
            public bool HasBeginningWildcard { get; set; }
            public bool HasEndingWildcard { get; set; }

            public BlacklistKeywordProperties(bool hasBeginningWildcard, bool hasEndingWildcard)
            {
                HasBeginningWildcard = hasBeginningWildcard;
                HasEndingWildcard = hasEndingWildcard;
            }
        }

        public ICommunicator Communicator { get; }
        public ISettingsService Settings { get; }
        public ILogger Logger { get; }
        public DiscordRestClient RestClient { get; }

        private static readonly TimeSpan MaxMessageProcessingDelay = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan CleanupTimer = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan ReportTimer = TimeSpan.FromMinutes(30);

        private readonly GlobalContext _context = new GlobalContext();

        private readonly ConcurrentDictionary<ulong, AhoCorasickDoubleArrayTrie<List<BlacklistKeywordProperties>>> BlacklistTries = 
            new ConcurrentDictionary<ulong, AhoCorasickDoubleArrayTrie<List<BlacklistKeywordProperties>>>();

        public RaidProtectionModule(ICommunicator communicator, ISettingsService settings, ILogger logger, Discord.Rest.DiscordRestClient restClient)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
            RestClient = restClient;
        }

        [Command("raid", "protection", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("raid", "protection"), Alias("raid-protection"), Alias("raid-protection", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: HelpBuilder.GetModuleHelpEmbed(this, command.Prefix));
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
                await command.ReplyError(Communicator, $"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            await Settings.Modify(command.GuildId, (RaidProtectionSettings x) =>
            {
                x.Enabled = true;
                x.LogChannel = command["LogChannel"].AsTextChannel.Id;
            });

            await command.ReplySuccess(Communicator, $"Raid protection has been enabled. Use `raid protection rules` to see the active rules.");
        }

        [Command("raid", "protection", "disable", "Disables raid protection.")]
        [Alias("raid-protection", "disable")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Comment("Does not erase your current rules.")]
        public async Task DisableRaidProtection(ICommand command)
        {
            await Settings.Modify(command.GuildId, (RaidProtectionSettings x) =>
            {
                x.Enabled = false;
                RefreshTrie(x);
            });
            await command.ReplySuccess(Communicator, $"Raid protection has been disabled.");
        }

        [Command("raid", "protection", "rules", "Displays active raid protection rules.")]
        [Alias("raid-protection", "rules")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        public async Task ListRulesRaidProtection(ICommand command)
        {
            var settings = await Settings.Read<RaidProtectionSettings>(command.GuildId);
            var result = new StringBuilder();
            result.AppendLine($"Protection **{(settings.Enabled ? "enabled" : "disabled")}**.");
            result.AppendLine($"Please make a request in the support server to modify any of these settings for your server (<{WebConstants.SupportServerInvite}>).");
            result.AppendLine("Phrase blacklist can be set with the `raid protection blacklist add` command.\n");

            var PrintDefaultFlag = new Func<RaidProtectionRuleType, string>(x => settings.IsDefault(x) ? " (default)" : "");
            result.AppendLine($"**MassMentionsRule** - if enabled, blocks messages containing more than {settings.MassMentionsRule.MentionsLimit} mentioned users" + PrintDefaultFlag(RaidProtectionRuleType.MassMentionsRule));
            result.AppendLine("`" + settings.MassMentionsRule + "`\n");

            result.AppendLine($"**TextSpamRule** - if enabled, blocks more than {settings.TextSpamRule.Threshold} messages sent in {settings.TextSpamRule.Window.TotalSeconds} seconds by one user" + PrintDefaultFlag(RaidProtectionRuleType.TextSpamRule));
            result.AppendLine("`" + settings.TextSpamRule + "`\n");

            result.AppendLine($"**ImageSpamRule** - if enabled, blocks more than {settings.ImageSpamRule.Threshold} images sent in {settings.ImageSpamRule.Window.TotalSeconds} seconds by one user" + PrintDefaultFlag(RaidProtectionRuleType.ImageSpamRule));
            result.AppendLine("`" + settings.ImageSpamRule + "`\n");

            result.AppendLine($"**PhraseBlacklistRule** - if enabled, blocks messages containing any of the specified phrases" + PrintDefaultFlag(RaidProtectionRuleType.PhraseBlacklistRule));
            result.AppendLine("`" + settings.PhraseBlacklistRule + "`\n");

            await command.Reply(Communicator, result.ToString());
        }

        [Command("raid", "protection", "set", "max", "offenses", "Sets how many violations of a rule may happen before the user gets muted.")]
        [Alias("raid-protection", "rules")]
        [Parameter("RuleName", ParameterType.String, "name of one of the rules from the `raid protection rules` command")]
        [Parameter("MaxOffenseCount", ParameterType.Int, "maximum number of offenses for this rule before the user gets muted")]
        [Permissions(GuildPermission.BanMembers)]
        public async Task SetMaxOffenseCount(ICommand command)
        {
            if (!Enum.TryParse<RaidProtectionRuleType>(command["RuleName"], out var type))
                throw new IncorrectParametersCommandException("Unknown rule type.");

            var maxOffenseCount = (int)command["MaxOffenseCount"];
            if (maxOffenseCount < 1)
                throw new IncorrectParametersCommandException("The value must be at least 1.", false);

            await Settings.Modify(command.GuildId, (RaidProtectionSettings x) =>
            {
                var rule = x.GetRule<RaidProtectionRule>(type);
                rule.MaxOffenseCount = maxOffenseCount;

                x.SetException(type, rule);
            });

            await command.ReplySuccess(Communicator, $"Users will now be muted after `{maxOffenseCount}` violations of `{type}`.");
        }

        [Command("raid", "protection", "blacklist", "add", "Adds one or more blacklisted phrases.")]
        [Alias("raid-protection", "blacklist", "add", true)]
        [Permissions(GuildPermission.BanMembers)]
        [Parameter("Phrases", ParameterType.String, ParameterFlags.Repeatable, "one or more phrases separated by spaces")]
        [Comment("Use the `*` wildcard before or after the phrase to also match longer phrases. For example, `darn*` also matches `darnit`. Matching is not case sensitive.\n\n Messages that match any of these phrases will be handled according to the PhraseBlacklist rule (default: the offending message will be deleted and upon commiting 3 offenses within 5 minutes the offending user will be muted).")]
        [Example("darn*")]
        [Example("\"fudge nugget\" *dang")]
        public async Task AddBlacklistRaidProtection(ICommand command)
        {
            const int minLength = 3;
            const int guildLimit = 500;

            var phrases = command["Phrases"].Repeats.Select(x => x.AsString).ToList();
            var tooShort = phrases.Where(x => x.Length < minLength);
            if (tooShort.Any())
                throw new IncorrectParametersCommandException($"A phrase needs to be at least {minLength} characters long. The following phrases are too short: {tooShort.WordJoinQuoted()}.", false);

            await Settings.Modify(command.GuildId, (RaidProtectionSettings x) =>
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

                RefreshTrie(x);
            });

            await command.ReplySuccess(Communicator, $"The following phrases have been added to the blacklist: {phrases.WordJoinQuoted()}.");
        }

        [Command("raid", "protection", "blacklist", "remove", "Removes one or more blacklisted phrases.")]
        [Alias("raid-protection", "blacklist", "remove", true)]
        [Permissions(GuildPermission.BanMembers)]
        [Parameter("Phrases", ParameterType.String, ParameterFlags.Repeatable, "one or more phrases separated by spaces")]
        [Example("darn*")]
        [Example("\"fudge nugget\" *dang")]
        public async Task RemoveBlacklistRaidProtection(ICommand command)
        {
            var phrases = command["Phrases"].Repeats.Select(x => x.AsString).ToList();
            await Settings.Modify(command.GuildId, (RaidProtectionSettings x) =>
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

                RefreshTrie(x);
            });

            await command.ReplySuccess(Communicator, $"The following phrases have been removed from the blacklist: {phrases.WordJoinQuoted()}.");
        }

        [Command("raid", "protection", "blacklist", "clear", "Removes all phrases from the blacklist.")]
        [Alias("raid-protection", "blacklist", "clear", true)]
        [Permissions(GuildPermission.BanMembers)]
        public async Task ClearBlacklistRaidProtection(ICommand command)
        {
            await Settings.Modify(command.GuildId, (RaidProtectionSettings x) =>
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

                RefreshTrie(x);
            });

            await command.ReplySuccess(Communicator, $"The phrase blacklist has been disabled.");
        }

        [Command("raid", "protection", "blacklist", "list", "Shows all blacklisted phrases.")]
        [Alias("raid-protection", "blacklist", "list", true)]
        [Permissions(GuildPermission.BanMembers)]
        public async Task ListBlacklistRaidProtection(ICommand command)
        {
            var settings = await Settings.Read<RaidProtectionSettings>(command.GuildId);

            if (!settings.PhraseBlacklistRule.Blacklist.Any())
            {
                await command.Reply(Communicator, "No blacklisted phrases have been added for this server.");
                return;
            }

            var builder = new PageCollectionBuilder(settings.PhraseBlacklistRule.Blacklist.Select(x => $"`{x}`"));
            var pages = builder.BuildEmbedCollection(() => new EmbedBuilder().WithTitle("Blacklisted phrases"), 25);

            await command.Reply(Communicator, pages);
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

            await Settings.Modify((ulong)command["ServerId"], (RaidProtectionSettings x) => x.SetException(type, newRule));
            await command.ReplySuccess(Communicator, $"Rule has been set.");
        }

        [Command("raid", "protection", "rules", "reset", "Resets a raid protection rule to default.", CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Parameter("ServerId", ParameterType.Id)]
        [Parameter("Type", ParameterType.String)]
        public async Task ResetRulesRaidProtection(ICommand command)
        {
            if (!Enum.TryParse<RaidProtectionRuleType>(command["Type"], out var type))
                throw new IncorrectParametersCommandException("Unknown rule type.");

            await Settings.Modify((ulong)command["ServerId"], (RaidProtectionSettings x) => x.ResetException(type));
            await command.ReplySuccess(Communicator, $"Rule has been reset.");
        }

        public override Task OnMessageReceived(SocketMessage message)
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
                    {
                        await Logger.Log(new LogMessage(LogSeverity.Warning, "Admin", $"Raid protection: user {message.Author.Id} is not a guild user in {channel.GuildId}"));
                        user = await RestClient.GetGuildUserAsync(channel.GuildId, message.Author.Id);

                        if (user == null)
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Error, "Admin", $"Raid protection: user {message.Author.Id} not found in guild {channel.GuildId}"));
                            return;
                        }
                    }

                    if (user.IsBot)
                        return;

                    var settings = await Settings.Read<RaidProtectionSettings>(channel.GuildId, false);
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
                        if (MatchBlacklist(channel.GuildId, message.Content, settings.PhraseBlacklistRule.Blacklist))
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
                        SpamRule appliedRule;
                        try
                        {
                            await userContext.Mutex.WaitAsync();

                            if ((string.IsNullOrEmpty(message.Content) && message.Attachments.Count > 0) ||
                                (!string.IsNullOrEmpty(message.Content) && Uri.IsWellFormedUriString(message.Content.Trim(), UriKind.Absolute)))
                            {
                                //Image post
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
                                //Text post
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

                    //Cleanup
                    if (_context.LastCleanup < DateTime.UtcNow - CleanupTimer)
                        await CleanupContext(settings);
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Admin", "Failed to process message for raid protection", ex));
                }
            });

            return Task.CompletedTask;
        }

        private bool MatchBlacklist(ulong guildId, string message, IEnumerable<string> blacklist)
        {
            var trie = BlacklistTries.GetOrAdd(guildId, x => CreateTrie(blacklist));

            bool matched = false;
            trie.ParseText(message, match =>
            {
                foreach (var keyword in match.Value)
                {
                    if (match.Begin > 0 && char.IsLetter(message[match.Begin - 1]) && !keyword.HasBeginningWildcard)
                        continue; // Inside a word (prefixed) - skip

                    if (match.End < message.Length && char.IsLetter(message[match.End]) && !keyword.HasEndingWildcard)
                        continue; // Inside a word (suffixed) - skip

                    matched = true;
                    return false; // Got a match - break
                }

                return true;
            });

            return matched;
        }

        private async Task CleanupContext(RaidProtectionSettings settings)
        {
            try
            {
                //Guild contexts
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
                        //User contexts
                        await guildContext.Value.Mutex.WaitAsync();
                        foreach (var userContext in guildContext.Value.UserContexts.ToList())
                        {
                            try
                            {
                                await userContext.Value.Mutex.WaitAsync();
                                foreach (var offenses in userContext.Value.Offenses.ToList())
                                {
                                    offenses.Value.SlideWindow(settings.GetRule<RaidProtectionRule>(offenses.Key).OffenseWindow, now - MaxMessageProcessingDelay);
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
                    var report = new StringBuilder();
                    report.AppendLine($"Raid protection status - {_context.GuildContexts.Count} guild contexts: ");
                    foreach (var guildContext in _context.GuildContexts)
                        report.AppendLine($"{guildContext.Key} ({guildContext.Value.UserContexts.Count}) ");

                    await Logger.Log(new LogMessage(LogSeverity.Info, "Admin", report.ToString()));
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

        private async Task EnforceRule(RaidProtectionRule rule, ICollection<IMessage> messages, ITextChannel channel, UserContext userContext, IGuildUser user, ulong logChannelId, string reason)
        {
            if (rule.Delete)
                foreach (var message in messages)
                    TaskHelper.FireForget(async () => await message.DeleteAsync());

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

                var currentUser = await channel.Guild.GetCurrentUserAsync();
                if (currentUser.GetPermissions(channel).SendMessages)
                {
                    IUserMessage warningMessage;
                    if (punish)
                    {
                        await AdministrationHelpers.Mute(user, "raid protection rule", Settings);
                        warningMessage = (await Communicator.SendMessage(channel, $"{user.Mention} you have been muted for breaking raid protection rules. If you believe this is a mistake, please contact a moderator.")).First();
                    }
                    else
                        warningMessage = (await Communicator.SendMessage(channel, $"{user.Mention} you have broken a raid protection rule.")).First();

                    warningMessage.DeleteAfter(8);
                }
                else
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Admin", $"Missing permissions to warn offender about rule {rule.Type} {(punish ? "(punished)" : "(warned)")} on user {user.Username} ({user.Id}) on {channel.Guild.Name} ({channel.Guild.Id})"));
                }

                var logChannel = await channel.Guild.GetTextChannelAsync(logChannelId);
                if (logChannel != null && currentUser.GetPermissions(logChannel).SendMessages)
                {
                    var embed = new EmbedBuilder()
                        .WithFooter(fb => fb.WithText(messages.Last().Timestamp.ToUniversalTime().ToString(@"yyyy\/MM\/dd H:mm:ss UTC")))
                        .AddField(fb => fb.WithName("Reason").WithValue($"Broken rule ({rule.Type})."));

                    if (punish)
                        embed.WithDescription($"**Muted user {user.Mention} for suspicious behavior:**\n" + reason).WithColor(Color.Red);
                    else
                        embed.WithDescription($"**Warned user {user.Mention} for suspicious behavior:**\n" + reason).WithColor(Color.Orange);

                    await logChannel.SendMessageAsync(string.Empty, embed: embed.Build());
                }
                else
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Admin", $"Couldn't report about rule {rule.Type} {(punish ? "(punished)" : "(warned)")} on user {user.Username} ({user.Id}) on {channel.Guild.Name} ({channel.Guild.Id})"));
                }

                await Logger.Log(new LogMessage(LogSeverity.Info, "Admin", $"Enforced rule {rule.Type} {(punish ? "(punished)" : "(warned)")} on user {user.Username} ({user.Id}) on {channel.Guild.Name} ({channel.Guild.Id}) because of {reason}"));
            }
        }

        private void RefreshTrie(RaidProtectionSettings settings)
        {
            if (settings.Enabled && settings.PhraseBlacklistRule.Enabled && settings.PhraseBlacklistRule.Blacklist.Any())
                BlacklistTries[settings.ServerId] = CreateTrie(settings.PhraseBlacklistRule.Blacklist);
            else
                BlacklistTries.TryRemove(settings.ServerId, out _);
        }

        private AhoCorasickDoubleArrayTrie<List<BlacklistKeywordProperties>> CreateTrie(IEnumerable<string> blacklist)
        {
            var keywords = blacklist
                .GroupBy(x => x.Trim('*', 1))
                .ToDictionary(x => x.Key, x => x.Select(y => new BlacklistKeywordProperties(y.First() == '*', y.Last() == '*')).ToList());

            return new AhoCorasickDoubleArrayTrie<List<BlacklistKeywordProperties>>(keywords, ignoreCase: true);
        }
    }
}
