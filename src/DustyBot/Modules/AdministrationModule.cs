using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Globalization;
using Newtonsoft.Json.Linq;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Settings;
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using Discord.WebSocket;
using System.Threading;
using DustyBot.Framework.Logging;
using System.Collections;

namespace DustyBot.Modules
{
    [Module("Administration", "Helps with server admin tasks.")]
    class AdministrationModule : Module
    {
        public const string MuteRoleName = "Muted";

        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public ILogger Logger { get; private set; }

        public AdministrationModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
        }

        [Command("say", "Sends a specified message.", CommandFlags.RunAsync)]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("TargetChannel", ParameterType.TextChannel, "a channel that will receive the message")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "the message to be sent (you may also include one attachment)")]
        public async Task Say(ICommand command)
        {
            if (command.Message.Attachments.Count <= 0)
            {
                if (string.IsNullOrEmpty(command["Message"]))
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Specify a message or an attachment.");

                await command[0].AsTextChannel.SendMessageAsync(command["Message"]);
            }
            else
            {
                var attachment = command.Message.Attachments.First();
                var request = WebRequest.CreateHttp(attachment.Url);
                using (var response = await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                using (var memStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memStream);
                    memStream.Position = 0;

                    await command[0].AsTextChannel.SendFileAsync(memStream, attachment.Filename, command["Message"]);
                }
            }

            if (command["TargetChannel"].AsTextChannel.Id != command.Message.Channel.Id)
                await command.ReplySuccess(Communicator, "Message sent.").ConfigureAwait(false);
        }

        [Command("edit", "Edits a message sent by the say command.", CommandFlags.RunAsync)]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.GuildSelfMessage, "a message previously sent by the `say` command")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder, "the message to send")]
        public async Task Edit(ICommand command)
        {
            var message = await command[0].AsGuildSelfMessage();
            await message.ModifyAsync(x => x.Content = command["Message"].AsString);
            await command.ReplySuccess(Communicator, "Message edited.").ConfigureAwait(false);
        }

        [Command("mute", "Mutes a server member.", CommandFlags.RunAsync)]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("User", ParameterType.GuildUser, "a server member to be muted")]
        [Parameter("Reason", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "reason")]
        public async Task Mute(ICommand command)
        {
            await Mute(command["User"].AsGuildUser, command["Reason"], Settings);
            await command.ReplySuccess(Communicator, $"User **{command["User"].AsGuildUser.Username}#{command["User"].AsGuildUser.DiscriminatorValue}** has been muted.").ConfigureAwait(false);
        }

        [Command("unmute", "Unmutes a server member.")]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("User", ParameterType.GuildUser, "a server member to be unmuted")]
        public async Task Unmute(ICommand command)
        {
            await Unmute(command["User"].AsGuildUser); 
            await command.ReplySuccess(Communicator, $"User **{command["User"].AsGuildUser.Username}#{command["User"].AsGuildUser.DiscriminatorValue}** has been unmuted.").ConfigureAwait(false);
        }

        [Command("raid-protection", "enable", "Protects the server against raids.")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles, GuildPermission.ManageMessages)]
        [Parameter("LogChannel", ParameterType.TextChannel, "a channel that will recieve notifications about performed actions")]
        [Comment("Upon enabling this feature, the bot will automatically delete obviously malicious messages and warn or mute offending users. The default rules are set up to only affect obvious raiders.")]
        public async Task EnableRaidProtection(ICommand command)
        {
            await Settings.Modify(command.GuildId, (RaidProtectionSettings x) => 
            {
                x.Enabled = true;
                x.LogChannel = command["LogChannel"].AsTextChannel.Id;
            });

            await command.ReplySuccess(Communicator, $"Raid protection has been enabled. Use `raid-protection rules` to see the active rules.").ConfigureAwait(false);
        }

        [Command("raid-protection", "disable", "Disables raid protection.")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Comment("Does not erase your current rules.")]
        public async Task DisableRaidProtection(ICommand command)
        {
            await Settings.Modify(command.GuildId, (RaidProtectionSettings x) => x.Enabled = false);
            await command.ReplySuccess(Communicator, $"Raid protection has been disabled.").ConfigureAwait(false);
        }

        [Command("raid-protection", "rules", "Displays active raid protection rules.")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        public async Task ListRulesRaidProtection(ICommand command)
        {
            var settings = await Settings.Read<RaidProtectionSettings>(command.GuildId);
            var result = new StringBuilder();
            result.AppendLine($"Protection **{(settings.Enabled ? "enabled" : "disabled")}**.");
            result.AppendLine("Please contact the bot owner to modify any of these settings for your server.");
            result.AppendLine("Phrase blacklist can be set with the `raid-protection blacklist` command.\n");

            var PrintDefaultFlag = new Func<RaidProtectionRuleType, string>(x => settings.IsDefault(x) ? " (default)" : "");
            result.AppendLine($"**MassMentionsRule** - if enabled, blocks messages containing more than {settings.MassMentionsRule.MentionsLimit} mentioned users" + PrintDefaultFlag(RaidProtectionRuleType.MassMentionsRule));
            result.AppendLine("`" + settings.MassMentionsRule + "`\n");
            
            result.AppendLine($"**TextSpamRule** - if enabled, blocks more than {settings.TextSpamRule.Threshold} messages sent in {settings.TextSpamRule.Window.TotalSeconds} seconds by one user" + PrintDefaultFlag(RaidProtectionRuleType.TextSpamRule));
            result.AppendLine("`" + settings.TextSpamRule + "`\n");

            result.AppendLine($"**ImageSpamRule** - if enabled, blocks more than {settings.ImageSpamRule.Threshold} images sent in {settings.ImageSpamRule.Window.TotalSeconds} seconds by one user" + PrintDefaultFlag(RaidProtectionRuleType.ImageSpamRule));
            result.AppendLine("`" + settings.ImageSpamRule + "`\n");

            result.AppendLine($"**PhraseBlacklistRule** - if enabled, blocks messages containing any of the specified phrases" + PrintDefaultFlag(RaidProtectionRuleType.PhraseBlacklistRule));
            result.AppendLine("`" + settings.PhraseBlacklistRule + "`\n");

            await command.Reply(Communicator, result.ToString()).ConfigureAwait(false);
        }

        [Command("raid-protection", "rules", "set", "Modifies a raid protection rule.", CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Parameter("ServerId", ParameterType.Id)]
        [Parameter("Type", ParameterType.String)]
        [Parameter("Rule", ParameterType.String, ParameterFlags.Remainder)]
        public async Task SetRulesRaidProtection(ICommand command)
        {
            RaidProtectionRuleType type;
            if (!Enum.TryParse(command["Type"], out type))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Unknown rule type.");

            RaidProtectionRule newRule;
            try
            {
                newRule = RaidProtectionRule.Create(type, command["Rule"]);
            }
            catch (Exception)
            {
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid rule.");
            }

            await Settings.Modify((ulong)command["ServerId"], (RaidProtectionSettings x) => x.SetException(type, newRule));
            await command.ReplySuccess(Communicator, $"Rule has been set.");
        }

        [Command("raid-protection", "rules", "reset", "Resets a raid protection rule to default.", CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Parameter("ServerId", ParameterType.Id)]
        [Parameter("Type", ParameterType.String)]
        public async Task ResetRulesRaidProtection(ICommand command)
        {
            RaidProtectionRuleType type;
            if (!Enum.TryParse(command["Type"], out type))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Unknown rule type.");

            await Settings.Modify((ulong)command["ServerId"], (RaidProtectionSettings x) => x.ResetException(type));
            await command.ReplySuccess(Communicator, $"Rule has been reset.");
        }

        [Command("raid-protection", "blacklist", "Sets blacklisted phrases.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Blacklist", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "blacklisted phrases, separated by colons (`,`)")]
        [Comment("Use without parameters to disable the blacklist. Messages that contain any of these phrases will be handled according to the PhraseBlacklist rule (default: the offending message will be deleted and upon commiting 3 offenses within 5 minutes the offending user will be muted).")]
        public async Task SetBlacklistRaidProtection(ICommand command)
        {
            var newList = await Settings.Modify(command.GuildId, (RaidProtectionSettings x) =>
            {
                var phrases = command["Blacklist"].AsString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                x.SetException(RaidProtectionRuleType.PhraseBlacklistRule, new PhraseBlacklistRule()
                {
                    Type = x.PhraseBlacklistRule.Type,
                    Enabled = phrases.Count > 0,
                    Delete = x.PhraseBlacklistRule.Delete,
                    MaxOffenseCount = x.PhraseBlacklistRule.MaxOffenseCount,
                    OffenseWindow = x.PhraseBlacklistRule.OffenseWindow,
                    Blacklist = phrases
                });
                
                return phrases;
            });

            if (newList.Count <= 0)
                await command.ReplySuccess(Communicator, "The phrase blacklist has been disabled.");
            else
                await command.ReplySuccess(Communicator, $"The phrase blacklist has been updated to the following: `{string.Join("`, `", newList)}`.");
        }

        [Command("server", "settings", "get", "Gets settings for a server.", CommandFlags.RunAsync | CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Parameter("ServerId", ParameterType.Id, ParameterFlags.Optional)]
        [Parameter("Module", ParameterType.String, "LiteDB collection name")]
        public async Task GetSettings(ICommand command)
        {
            var channel = await command.Message.Author.GetOrCreateDMChannelAsync();
            var result = await Settings.DumpSettings(command[0].AsId ?? command.GuildId, command["Module"]);
            await Communicator.CommandReply(channel, result, x => $"```{x}```", 6).ConfigureAwait(false);
        }

        public static async Task Mute(IGuildUser user, string reason, ISettingsProvider settings)
        {
            IRole muteRole = user.Guild.Roles.FirstOrDefault(x => x.Name == MuteRoleName);
            if (muteRole == null)
            {
                muteRole = await user.Guild.CreateRoleAsync(MuteRoleName, GuildPermissions.None);
                if (muteRole == null)
                    throw new InvalidOperationException("Cannot create mute role.");
            }

            foreach (var channel in await user.Guild.GetChannelsAsync())
            {
                var overwrite = channel.GetPermissionOverwrite(muteRole);
                if (overwrite == null || overwrite.Value.SendMessages != PermValue.Deny || overwrite.Value.Connect != PermValue.Deny || overwrite.Value.AddReactions != PermValue.Deny)
                    await channel.AddPermissionOverwriteAsync(muteRole, new OverwritePermissions(sendMessages: PermValue.Deny, connect: PermValue.Deny, addReactions: PermValue.Deny));
            }

            if ((await settings.Read<RolesSettings>(user.GuildId, false)).AdditionalPersistentRoles.Contains(muteRole.Id) == false)
                await settings.Modify(user.GuildId, (RolesSettings x) => x.AdditionalPersistentRoles.Add(muteRole.Id));

            await user.AddRoleAsync(muteRole, new RequestOptions() { AuditLogReason = reason });
        }

        public static async Task Unmute(IGuildUser user)
        {
            IRole muteRole = user.Guild.Roles.FirstOrDefault(x => x.Name == MuteRoleName);
            if (muteRole == null)
                return;

            await user.RemoveRoleAsync(muteRole);
        }

        class SlidingMessageCache : ICollection<IMessage>
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

        abstract class BaseContext
        {
            public SemaphoreSlim Mutex { get; } = new SemaphoreSlim(1, 1);
        }

        class GlobalContext : BaseContext
        {
            public DateTime LastCleanup { get; set; } = DateTime.MinValue;
            public DateTime LastReport { get; set; } = DateTime.MinValue;
            public Dictionary<ulong, GuildContext> GuildContexts { get; } = new Dictionary<ulong, GuildContext>();
        }

        class GuildContext : BaseContext
        {
            public Dictionary<ulong, UserContext> UserContexts { get; } = new Dictionary<ulong, UserContext>();
        }

        class UserContext : BaseContext
        {
            public Dictionary<RaidProtectionRuleType, SlidingMessageCache> Offenses { get; } = new Dictionary<RaidProtectionRuleType, SlidingMessageCache>();
            public SlidingMessageCache ImagePosts { get; } = new SlidingMessageCache();
            public SlidingMessageCache TextPosts { get; } = new SlidingMessageCache();

            public bool Empty => Offenses.Count <= 0 && ImagePosts.Count <= 0 && TextPosts.Count <= 0;
        }

        GlobalContext _context = new GlobalContext();
        static readonly TimeSpan MaxMessageProcessingDelay = TimeSpan.FromMinutes(2);
        static readonly TimeSpan CleanupTimer = TimeSpan.FromMinutes(2);
        static readonly TimeSpan ReportTimer = TimeSpan.FromMinutes(30);

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
                        return;

                    if (user.IsBot)
                        return;

                    var settings = await Settings.Read<RaidProtectionSettings>(channel.GuildId, false).ConfigureAwait(false);
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
                        if (settings.PhraseBlacklistRule.Blacklist.Any(x => message.Content.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
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

        async Task CleanupContext(RaidProtectionSettings settings)
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

        async Task<UserContext> GetUserContext(IGuildUser user)
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

        async Task EnforceRule(ReadOnlyRaidProtectionRule rule, ICollection<IMessage> messages, ITextChannel channel, UserContext userContext, IGuildUser user, ulong logChannelId, string reason)
        {
            if (rule.Delete)
                foreach (var message in messages)
                    TaskHelper.FireForget(async() => await message.DeleteAsync());

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

                IUserMessage warningMessage;
                if (punish)
                {
                    await Mute(user, "raid protection rule", Settings);
                    warningMessage = (await Communicator.SendMessage(channel, $"{user.Mention} you have been muted for breaking raid protection rules. If you believe this is a mistake, please contact a moderator.")).First();
                }
                else
                    warningMessage = (await Communicator.SendMessage(channel, $"{user.Mention} you have broken a raid protection rule.")).First();

                warningMessage.DeleteAfter(8);

                var embed = new EmbedBuilder()
                    .WithFooter(fb => fb.WithText(messages.Last().Timestamp.ToUniversalTime().ToString("dd.MM.yyyy H:mm:ss UTC")))
                    .AddField(fb => fb.WithName("Reason").WithValue($"Broken rule ({rule.Type})."));

                if (punish)
                    embed.WithDescription($"**Muted user {user.Mention} for suspicious behavior:**\n" + reason).WithColor(Color.Red);
                else
                    embed.WithDescription($"**Warned user {user.Mention} for suspicious behavior:**\n" + reason).WithColor(Color.Orange);

                var logChannel = await channel.Guild.GetTextChannelAsync(logChannelId);
                if (logChannel != null)
                    await logChannel.SendMessageAsync(string.Empty, embed: embed.Build());

                await Logger.Log(new LogMessage(LogSeverity.Info, "Admin", $"Enforced rule {rule.Type} {(punish ? "(punished)" : "(warned)")} on user {user.Username} ({user.Id}) on {channel.Guild.Name} because of {reason}"));
            }
        }
    }
}
