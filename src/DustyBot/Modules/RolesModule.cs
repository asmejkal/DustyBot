using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Utility;
using DustyBot.Framework.Logging;
using DustyBot.Settings;
using Discord.WebSocket;
using DustyBot.Definitions;
using DustyBot.Framework.Exceptions;
using System.Threading;
using DustyBot.Core.Async;
using DustyBot.Core.Collections;
using DustyBot.Core.Formatting;
using DustyBot.Database.Services;
using System.Collections.Concurrent;

namespace DustyBot.Modules
{
    [Module("Roles", "Let users choose their own roles  – please check out the <a href=\"" + WebConstants.RolesGuideUrl + "\">guide</a>.")]
    class RolesModule : Module
    {
        private class RoleStats : IDisposable
        {
            private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
            private readonly ILogger _logger;
            private DateTimeOffset _updated;
            private Dictionary<ulong, int> _data;
            private ulong _boundGuildId;

            public RoleStats(ILogger logger)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public async Task<(IReadOnlyDictionary<ulong, int> Data, DateTimeOffset? WhenCached)> GetOrFillAsync(SocketGuild guild, TimeSpan maxAge, Func<double, Task> onProgress = null)
            {
                if (maxAge < TimeSpan.Zero)
                    throw new ArgumentException("Can't be negative", nameof(maxAge));

                using (await _lock.ClaimAsync())
                {
                    if (_data != null && _updated + maxAge > DateTimeOffset.UtcNow && _boundGuildId == guild.Id)
                        return (_data, _updated);

                    _data = new Dictionary<ulong, int>();
                    _updated = DateTimeOffset.UtcNow;
                    _boundGuildId = guild.Id;

                    foreach (var role in guild.Roles.Select(x => x.Id))
                        _data[role] = 0;

                    if (onProgress != null)
                        await onProgress(0);

                    var processed = 0;
                    var batches = 0;
                    await foreach (var batch in guild.GetUsersAsync())
                    {
                        await _logger.Log(new LogMessage(LogSeverity.Info, "Roles", $"Fetched {batch.Count} users on guild {guild.Id} ({guild.Name})"));
                        foreach (var user in batch)
                        {
                            foreach (var role in user.RoleIds)
                            {
                                if (_data.TryGetValue(role, out var value)) 
                                    _data[role] = value + 1;

                                // No else to track only existing roles (sometimes Discord is stupid and keeps deleted roles on users)
                            }
                        }

                        processed += batch.Count;
                        batches++;
                        if (onProgress != null && batches % 3 == 0)
                            await onProgress((double)processed / guild.MemberCount);
                    }

                    return (_data, null);
                }
            }

            public void Dispose() => _lock.Dispose();
        }

        private static readonly TimeSpan MaxRoleStatsAge = TimeSpan.FromHours(1);

        public ICommunicator Communicator { get; }
        public ISettingsService Settings { get; }
        public ILogger Logger { get; }

        private readonly KeyedSemaphoreSlim<ulong> _roleAssignmentUserMutex = new KeyedSemaphoreSlim<ulong>(1);
        private readonly ConcurrentDictionary<ulong, RoleStats> _roleStatsCache = new ConcurrentDictionary<ulong, RoleStats>();

        public RolesModule(ICommunicator communicator, ISettingsService settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
        }

        [Command("roles", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("role", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(Communicator, $"Check out the guide at <{WebConstants.RolesGuideUrl}>!");
        }

        [Command("roles", "set", "channel", "Sets a channel for role self-assignment.", CommandFlags.Synchronous)]
        [Alias("roles", "channel", true), Alias("role", "channel", true)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Remainder)]
        public async Task SetRoleChannel(ICommand command)
        {
            var permissions = (await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel);
            if (!permissions.SendMessages)
            {
                await command.ReplyError(Communicator, $"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                if (s.ClearRoleChannel && !permissions.ManageMessages)
                    throw new CommandException("Automatic message clearing is enabled, but the bot does not have the ManageMessages permission for this channel.");

                s.RoleChannel = command["Channel"].AsTextChannel.Id;
            });
                
            await command.ReplySuccess(Communicator, "Role channel has been set.");
        }

        [Command("roles", "toggle", "clearing", "Toggles automatic clearing of role channel.", CommandFlags.Synchronous)]
        [Alias("role", "toggle", "clearing", true), Alias("roles", "clearing"), Alias("role", "clearing", true)]
        [Permissions(GuildPermission.ManageMessages)]
        [Comment("Disabled by default.")]
        public async Task SetRoleChannelClearing(ICommand command)
        {
            var settings = await Settings.Read<RolesSettings>(command.GuildId);
            if (!settings.ClearRoleChannel && settings.RoleChannel != 0)
            {
                var channel = await command.Guild.GetChannelAsync(settings.RoleChannel);
                if (channel != null && (await channel.Guild.GetCurrentUserAsync()).GetPermissions(channel).ManageMessages == false)
                {
                    await command.ReplyError(Communicator, $"To enable automatic clearing, the bot needs the ManageMessages permission for the role channel ({channel.Name}).");
                    return;
                }
            }

            bool result = await Settings.Modify(command.GuildId, (RolesSettings s) => s.ClearRoleChannel = !s.ClearRoleChannel);
            await command.ReplySuccess(Communicator, $"Automatic role channel clearing has been " + (result ? "enabled" : "disabled") + ".");
        }

        [Command("roles", "toggle", "persistence", "Restore self-assignable roles upon rejoining the server.")]
        [Alias("role", "toggle", "persistence", true), Alias("roles", "persistence"), Alias("role", "persistence", true)]
        [Permissions(GuildPermission.Administrator)]
        [Comment("Toggle. All self-assignable roles the user had upon leaving will be reapplied if they rejoin. The feature had to be turned on when the user left for it to function.")]
        public async Task PersistentRoles(ICommand command)
        {
            var selfUser = await command.Guild.GetCurrentUserAsync();
            var newVal = await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                if (!s.PersistentAssignableRoles && selfUser.GuildPermissions.ManageRoles == false)
                    throw new MissingBotPermissionsException(GuildPermission.ManageRoles);

                return s.PersistentAssignableRoles = !s.PersistentAssignableRoles;
            });

            await command.ReplySuccess(Communicator, $"Self-assignable roles will {(newVal ? "now" : "no longer")} be restored for users who leave and rejoin the server.");
        }

        [Command("roles", "create", "Creates a new self-assignable role.")]
        [Alias("role", "create")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("Name", ParameterType.String, ParameterFlags.Remainder, "what to name the role which will be created")]
        [Comment("Any user can then assign this role to themselves by typing its name or alias (without any prefix) in the channel set by the `roles channel` command. The role can be also self-removed by typing `-` followed by its name or alias (eg. `-Solar`).")]
        [Example("Solar")]
        public async Task CreateRole(ICommand command)
        {
            var role = await command.Guild.CreateRoleAsync(command["Name"], permissions: new GuildPermissions(), isMentionable: false);
            await AddRole(command.GuildId, role, false);

            await command.ReplySuccess(Communicator, $"A self-assignable role `{command["Name"]}` has been created. You can set a color or reorder it in the server's settings.");
        }

        [Command("roles", "add", "Adds an already existing self-assignable role.")]
        [Alias("role", "add")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("RoleNameOrID", ParameterType.Role, ParameterFlags.Remainder, "A name or ID of the self-assignable role.")]
        [Comment("Any user can then assign this role to themselves by typing its name or alias (without any prefix) in the channel set by the `roles channel` command. The role can be also self-removed by typing `-` followed by its name or alias (eg. `-Solar`).\n\nRoles are not case sensitive, but roles with matching casing get assigned before others.")]
        [Example("Solar")]
        public async Task AddRole(ICommand command)
        {
            await AddRole(command.GuildId, command["RoleNameOrID"].AsRole, true);

            await command.ReplySuccess(Communicator, "Self-assignable role added.");
        }

        [Command("roles", "remove", "Removes a self-assignable role.")]
        [Alias("role", "remove")]
        [Permissions(GuildPermission.ManageRoles)]
        [Parameter("RoleNameOrID", ParameterType.Role, ParameterFlags.Remainder)]
        public async Task RemoveRole(ICommand command)
        {
            bool removed = await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                return s.AssignableRoles.RemoveAll(x => x.RoleId == command[0].AsRole.Id) > 0;
            });

            if (!removed)
                await command.ReplyError(Communicator, $"This role is not self-assignable.");
            else
                await command.ReplySuccess(Communicator, $"Self-assignable role removed.");
        }

        [Command("roles", "list", "Lists all self-assignable roles.")]
        [Alias("role", "list")]
        [Permissions(GuildPermission.ManageRoles)]
        public async Task ListAutoRoles(ICommand command)
        {
            var settings = await Settings.Read<RolesSettings>(command.GuildId);

            var result = new StringBuilder();
            foreach (var role in settings.AssignableRoles)
            {
                var guildRole = command.Guild.Roles.FirstOrDefault(x => x.Id == role.RoleId);
                if (guildRole == default)
                    continue;

                result.Append($"\nRole: `{guildRole.Name}` ");

                if (role.Names.Any())
                    result.Append($"Aliases: `" + string.Join(", ", role.Names) + "` ");

                if (role.SecondaryId != 0)
                    result.Append($"Secondary: `{command.Guild.Roles.FirstOrDefault(x => x.Id == role.SecondaryId)?.Name ?? "DELETED ROLE"}` ");

                if (role.Groups.Any())
                    result.Append($"Groups: {role.Groups.WordJoinQuoted(separator: " ", lastSeparator: " ")} ");
            }

            if (result.Length <= 0)
                result.Append("No self-assignable roles have been set up.");

            await command.Reply(Communicator, result.ToString());
        }

        [Command("roles", "alias", "add", "Adds an alias for a self-assignable role.")]
        [Alias("role", "alias", "add")]
        [Permissions(GuildPermission.ManageRoles)]
        [Parameter("RoleNameOrID", ParameterType.Role, "A currently self-assignable role")]
        [Parameter("Alias", ParameterType.String, ParameterFlags.Remainder, "An alias that can be used to assign this role instead")]
        [Example("Solar Yeba")]
        public async Task AddAlias(ICommand command)
        {
            await Settings.Modify(command.GuildId, (RolesSettings s) => 
            {
                var role = s.AssignableRoles.FirstOrDefault(x => x.RoleId == command["RoleNameOrId"].AsRole.Id);
                if (role == null)
                    throw new CommandException($"Role `{command["RoleNameOrId"].AsRole.Id}` is not self-assignable.  Add it first with `roles add`.");

                if (s.AssignableRoles.SelectMany(x => x.Names).Contains(command["Alias"]))
                    throw new CommandException("A self-assignable role with this name or alias already exists");

                role.Names.Add(command["Alias"]);
            });

            await command.ReplySuccess(Communicator, $"Added alias `{command["Alias"]}` to role `{command["RoleNameOrID"].AsRole.Name}`.");
        }

        [Command("roles", "alias", "remove", "Removes an alias.")]
        [Alias("role", "alias", "remove")]
        [Permissions(GuildPermission.ManageRoles)]
        [Parameter("RoleNameOrID", ParameterType.Role, "A currently self-assignable role")]
        [Parameter("Alias", ParameterType.String, ParameterFlags.Remainder, "The alias to remove")]
        [Example("Solar Yeba")]
        public async Task RemoveAlias(ICommand command)
        {
            int removed = await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                var role = s.AssignableRoles.FirstOrDefault(x => x.RoleId == command["RoleNameOrId"].AsRole.Id);
                if (role == null)
                    throw new CommandException($"Role `{command["RoleNameOrId"].AsRole.Id}` is not self-assignable.");

                return role.Names.RemoveAll(x => x == command["Alias"]);
            });

            if (removed <= 0)
                await command.ReplyError(Communicator, $"No alias found with this name for role `{command["RoleNameOrId"].AsRole.Id}`.");
            else
                await command.ReplySuccess(Communicator, $"Alias `{command["Alias"]}` removed.");
        }

        [Command("roles", "set", "secondary", "Sets a primary-secondary bias role pair.")]
        [Alias("role", "set", "secondary"), Alias("role", "setbias", true), Alias("roles", "setbias", true)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("PrimaryRoleNameOrID", ParameterType.Role)]
        [Parameter("SecondaryRoleNameOrID", ParameterType.Role)]
        [Comment("Marks a role as a primary bias role and links it with a secondary role. If a user already has **any** primary bias role assigned, then the bot will assign this secondary role instead. This means that the first bias role a user sets will be their primary. After that, any other bias role they assign will become secondary. They may change their primary bias by removing the primary bias and assigning a new one.\n\nIf you run:\n`{p}roles add Solar`\n`{p}roles add Wheein`\n`{p}roles set secondary Solar .Solar`\n`{p}roles set secondary Wheein .Wheein`\n\nThen typing this in the role channel:\n`Solar`\n`Wheein`\n\nWill result in the user having a primary `Solar` role and a secondary `.Wheein` role.")]
        [Example("Solar .Solar")]
        public async Task SetBiasRole(ICommand command)
        {
            var primary = command["PrimaryRoleNameOrID"].AsRole;
            var secondary = command["SecondaryRoleNameOrID"].AsRole;
            if (primary.Id == secondary.Id)
            {
                await command.ReplyError(Communicator, $"The primary and secondary roles can't be the same role.");
                return;
            }

            if (!secondary.CanUserAssign(await command.Guild.GetCurrentUserAsync()))
                throw new CommandException($"The bot doesn't have permission to assign the secondary role to users. Please make sure the role is below the bot's highest role in the server's role list.");

            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                var assignableRole = s.AssignableRoles.FirstOrDefault(x => x.RoleId == primary.Id);
                if (assignableRole == null)
                    throw new CommandException("The primary role is not self-assignable.");

                if (s.AssignableRoles.Any(x => x.RoleId == secondary.Id))
                    throw new CommandException("The secondary role is already self-assignable by itself. Remove it from self-assignable roles with `roles remove` first.");

                assignableRole.SecondaryId = secondary.Id;
            });

            await command.ReplySuccess(Communicator, $"Role `{primary.Name} ({primary.Id})` has been set as a primary bias role to `{secondary.Name} ({secondary.Id})`.");
        }

        [Command("roles", "stats", "Server roles statistics.", CommandFlags.TypingIndicator)]
        [Alias("role", "stats"), Alias("bias", "stats")]
        [Parameter("all", "all", ParameterFlags.Optional, "include non-assignable roles")]
        public async Task RolesStats(ICommand command)
        {
            var guild = (SocketGuild)command.Guild;

            IUserMessage progressMessage = null;
            Func<double, Task> onProgress = null;
            if (guild.MemberCount > 5000)
            {
                onProgress = new Func<double, Task>(async x =>
                {
                    if (progressMessage == null)
                        progressMessage = (await command.Reply(Communicator, $"Processing guild members... {Math.Ceiling(x * 100)}%")).First();
                    else
                        await progressMessage.ModifyAsync(m => m.Content = $"Processing guild members... {Math.Ceiling(x * 100)}%");
                });
            }

            var stats = _roleStatsCache.GetOrAdd(command.GuildId, x => new RoleStats(Logger));
            var (data, whenCached) = await stats.GetOrFillAsync(guild, MaxRoleStatsAge, onProgress);

            if (progressMessage != null)
                await progressMessage.DeleteAsync();

            var dataAge = DateTimeOffset.UtcNow - whenCached;

            var pages = new PageCollection();
            const int MaxLines = 30;
            if (command["all"].HasValue)
            {
                int count = 0;
                foreach (var kv in data.OrderByDescending(x => x.Value))
                {
                    if (command.Guild.EveryoneRole.Id == kv.Key)
                        continue;

                    if (count++ % MaxLines == 0)
                        pages.Add(new EmbedBuilder().WithTitle("All roles").WithDescription(string.Empty));

                    pages.Last.Embed.Description += $"<@&{kv.Key}> — {kv.Value} user{(kv.Value != 1 ? "s" : "")}\n";
                    if (dataAge.HasValue)
                        pages.Last.Embed.WithFooter(x => x.Text = $"Results from {dataAge.Value.SimpleFormat()} ago");
                }
            }
            else
            {
                var settings = await Settings.Read<RolesSettings>(command.GuildId, false);
                if (settings == null || settings.AssignableRoles.Count <= 0)
                {
                    await command.Reply(Communicator, "No self-assignable roles have been set up. For statistics of all roles use `roles stats all`.");
                    return;
                }

                int count = 0;
                foreach (var kv in data.OrderByDescending(x => x.Value))
                {
                    var role = settings.AssignableRoles.FirstOrDefault(x => x.RoleId == kv.Key);
                    if (role == null)
                        continue;

                    var guildRole = command.Guild.GetRole(role.RoleId);
                    if (guildRole == null)
                        continue;

                    if (count++ % MaxLines == 0)
                        pages.Add(new EmbedBuilder().WithTitle("Assignable roles").WithDescription(string.Empty));

                    pages.Last.Embed.Description += $"{guildRole.Mention} — {kv.Value} user{(kv.Value != 1 ? "s" : "")}\n";
                    if (dataAge.HasValue)
                        pages.Last.Embed.WithFooter(x => x.Text = $"Results from {dataAge.Value.SimpleFormat()} ago");

                    if (role.SecondaryId != default)
                    {
                        pages.Last.Embed.Description += $" ┕ _Secondary_ — {data[role.SecondaryId]} users\n";

                        if (count % MaxLines != 0)
                            count++;
                    }
                }
            }

            await command.Reply(Communicator, pages);
        }

        [Command("roles", "disable", "Disables role self-assignment.", CommandFlags.Synchronous)]
        [Alias("roles", "channel", "reset")]
        [Permissions(GuildPermission.Administrator)]
        [Comment("Your configured roles will not get deleted. To enable again, use `roles set channel`.")]
        public async Task Disable(ICommand command)
        {
            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                s.RoleChannel = default;
            });

            await command.ReplySuccess(Communicator, "Role channel has been disabled.");
        }

        [Command("roles", "group", "add", "Adds one or more roles into a group.", CommandFlags.Synchronous)]
        [Alias("role", "group", "add")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("GroupName", ParameterType.String, "name of the group")]
        [Parameter("Roles", ParameterType.Role, ParameterFlags.Repeatable, "one or more roles (names or IDs) that will be added to the group")]
        [Comment("All the provided roles must be self-assignable (added with `roles add` or `roles create`).")]
        [Example("primaries solar wheein moonbyul hwasa")]
        public async Task AddRoleGroup(ICommand command)
        {
            var roles = command["Roles"].Repeats.Select(x => x.AsRole);
            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                foreach (var role in roles)
                {
                    var assignableRole = s.AssignableRoles.FirstOrDefault(x => x.RoleId == role.Id);
                    if (assignableRole == default)
                        throw new CommandException($"Role `{role.Name}` is not self-assignable. Please add it with `roles add` first.");

                    assignableRole.Groups.Add(command["GroupName"]);
                }
            });

            await command.ReplySuccess(Communicator, $"Role{(roles.Skip(1).Any() ? "s" : "")} {roles.Select(x => x.Name).WordJoinQuoted()} {(roles.Skip(1).Any() ? "have" : "has")} been added to group `{command["GroupName"]}`.");
        }

        [Command("roles", "group", "remove", "Removes one or more roles from a group.", CommandFlags.Synchronous)]
        [Alias("role", "group", "remove")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("GroupName", ParameterType.String, "name of the group")]
        [Parameter("Roles", ParameterType.Role, ParameterFlags.Repeatable, "one or more roles (names or IDs) that will be removed from the group")]
        [Example("primaries solar wheein moonbyul hwasa")]
        public async Task RemoveRoleGroup(ICommand command)
        {
            var roles = command["Roles"].Repeats.Select(x => x.AsRole);
            var removed = new List<string>();
            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                foreach (var role in roles)
                {
                    var assignableRole = s.AssignableRoles.FirstOrDefault(x => x.RoleId == role.Id);
                    if (assignableRole == default)
                        continue;

                    if (assignableRole.Groups.Remove(command["GroupName"]))
                        removed.Add(role.Name);
                }
            });

            await command.ReplySuccess(Communicator, $"Role{(roles.Skip(1).Any() ? "s" : "")} {removed.WordJoinQuoted()} {(roles.Skip(1).Any() ? "have" : "has")} been removed from group `{command["GroupName"]}`.");
        }

        [Command("roles", "group", "clear", "Removes all roles from a group.", CommandFlags.Synchronous)]
        [Alias("role", "group", "clear")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("GroupName", ParameterType.String, "name of the group")]
        public async Task ClearRoleGroup(ICommand command)
        {
            var removed = new List<string>();
            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                foreach (var role in s.AssignableRoles)
                {
                    if (role.Groups.Remove(command["GroupName"]))
                        removed.Add(command.Guild.GetRole(role.RoleId)?.Name ?? role.RoleId.ToString());
                }
            });

            await command.ReplySuccess(Communicator, $"Group `{command["GroupName"]}` has been cleared.");
        }

        [Command("roles", "group", "set", "limit", "Sets a limit on how many roles can be assigned from a given group.", CommandFlags.Synchronous)]
        [Alias("role", "group", "set", "limit")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("GroupName", ParameterType.String, "name of the group created with `roles group add`")]
        [Parameter("Limit", ParameterType.UInt, "the limit; put `0` for no limit")]
        [Example("primaries 1")]
        public async Task SetRoleGroupLimit(ICommand command)
        {
            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                if (!s.AssignableRoles.Any(x => x.Groups.Contains(command["GroupName"])))
                    throw new CommandException($"There's no role in group `{command["GroupName"]}` or it doesn't exist. Please add some roles first with `roles group add`.");

                s.GroupSettings.GetOrCreate(command["GroupName"]).Limit = command["Limit"].AsUInt.Value;
            });

            await command.ReplySuccess(Communicator, $"Users may now assign up to `{command["Limit"].AsUInt.Value}` roles from group `{command["GroupName"]}`.");
        }

        private async Task AddRole(ulong guildId, IRole role, bool checkPermissions)
        {
            if (checkPermissions && !role.CanUserAssign(await role.Guild.GetCurrentUserAsync()))
                throw new CommandException($"The bot doesn't have permission to assign this role to users. Please make sure the role is below the bot's highest role in the server's role list.");

            var newRole = new AssignableRole();
            newRole.RoleId = role.Id;

            await Settings.Modify(guildId, (RolesSettings s) =>
            {
                if (s.AssignableRoles.Any(x => x.RoleId == role.Id))
                    throw new CommandException("This role is already self-assignable.");

                if (s.AssignableRoles.Any(x => x.SecondaryId == role.Id))
                    throw new CommandException("This role is already set as a secondary to another role.");

                s.AssignableRoles.Add(newRole);
            });
        }

        public override Task OnUserLeft(SocketGuildUser guildUser)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var settings = await Settings.Read<RolesSettings>(guildUser.Guild.Id, false);
                    if (settings == null)
                        return;

                    if (!settings.PersistentAssignableRoles && settings.AdditionalPersistentRoles.Count <= 0)
                        return;

                    var roles = guildUser.Roles.Where(x => settings.AdditionalPersistentRoles.Contains(x.Id)).ToList();
                    if (settings.PersistentAssignableRoles)
                        roles.AddRange(guildUser.Roles.Where(x => settings.AssignableRoles.Any(y => y.RoleId == x.Id || y.SecondaryId == x.Id)));

                    if (roles.Count <= 0)
                        return;

                    await Logger.Log(new LogMessage(LogSeverity.Info, "Roles", $"Saving {roles.Count} roles for user {guildUser.Username} ({guildUser.Id}) on {guildUser.Guild.Name}"));

                    await Settings.Modify(guildUser.Guild.Id, (RolesSettings x) =>
                    {
                        var userRoles = x.PersistentRolesData.GetOrCreate(guildUser.Id);
                        userRoles.Clear();
                        userRoles.AddRange(roles.Select(y => y.Id));
                    });
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Roles", $"Failed to save persistent roles for user {guildUser.Username} ({guildUser.Id}) on {guildUser.Guild.Name}", ex));
                }
            });

            return Task.CompletedTask;
        }

        public override Task OnUserJoined(SocketGuildUser guildUser)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var settings = await Settings.Read<RolesSettings>(guildUser.Guild.Id, false);
                    if (settings == null)
                        return;

                    if (!settings.PersistentAssignableRoles && settings.AdditionalPersistentRoles.Count <= 0)
                        return;

                    List<ulong> roleIds;
                    if (!settings.PersistentRolesData.TryGetValue(guildUser.Id, out roleIds))
                        return;

                    // Intersect with current persistent roles
                    var intersected = roleIds.Where(x => settings.AdditionalPersistentRoles.Contains(x)).ToList();
                    if (settings.PersistentAssignableRoles)
                        intersected.AddRange(roleIds.Where(x => settings.AssignableRoles.Any(y => y.RoleId == x || y.SecondaryId == x)));

                    var roles = intersected.Select(x => guildUser.Guild.Roles.FirstOrDefault(y => x == y.Id)).Where(x => x != null).ToList();

                    await Logger.Log(new LogMessage(LogSeverity.Info, "Roles", $"Restoring {roles.Count} roles for user {guildUser.Username} ({guildUser.Id}) on {guildUser.Guild.Name}"));

                    if (roles.Count > 0)
                        await guildUser.AddRolesAsync(roles);

                    await Settings.Modify(guildUser.Guild.Id, (RolesSettings x) => x.PersistentRolesData.Remove(guildUser.Id));
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Roles", $"Failed to save persistent roles for user {guildUser.Username} ({guildUser.Id}) on {guildUser.Guild.Name}", ex));
                }
            });

            return Task.CompletedTask;
        }

        public override Task OnMessageReceived(SocketMessage message)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var channel = message.Channel as SocketTextChannel;
                    if (channel == null)
                        return;

                    var user = message.Author as IGuildUser;
                    if (user == null)
                        return;

                    if (user.IsBot)
                        return;

                    using (await _roleAssignmentUserMutex.ClaimAsync(user.Id)) // To prevent race-conditions when spamming roles
                    {
                        var settings = await Settings.Read<RolesSettings>(channel.Guild.Id, false);
                        if (settings == null || settings.RoleChannel != channel.Id)
                            return;

                        if (!channel.Guild.CurrentUser.GetPermissions(channel).SendMessages)
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Info, "Roles", $"Can't assign role because of missing SendMessage permissions in #{channel.Name} ({channel.Id}) on {channel.Guild.Name} ({channel.Guild.Id})"));
                            return;
                        }

                        try
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Info, "Roles", $"\"{message.Content}\" by {message.Author.Username} ({message.Author.Id}) on {channel.Guild.Name} ({channel.Guild.Id})"));

                            string msgContent = message.Content.Trim();
                            bool remove = false;
                            if (msgContent.StartsWith("-"))
                            {
                                msgContent = msgContent.Substring(1);
                                remove = true;
                            }

                            msgContent = msgContent.TrimStart('-', '+');
                            msgContent = msgContent.Trim();

                            // First try to match an alias case sensitive
                            var roleAar = settings.AssignableRoles.FirstOrDefault(x => x.Names.Any(y => string.Compare(y, msgContent) == 0) && channel.Guild.GetRole(x.RoleId) != null);

                            // Then try current role names case sensitive
                            if (roleAar == null)
                            {
                                roleAar = settings.AssignableRoles
                                    .Select(x => (Aar: x, Role: channel.Guild.GetRole(x.RoleId)))
                                    .FirstOrDefault(x => x.Role != null && string.Compare(x.Role.Name, msgContent) == 0)
                                    .Aar;
                            }

                            // Then alias case insensitive
                            if (roleAar == null)
                                roleAar = settings.AssignableRoles.FirstOrDefault(x => x.Names.Any(y => string.Compare(y, msgContent, true, GlobalDefinitions.Culture) == 0) && channel.Guild.GetRole(x.RoleId) != null);

                            // And current role names case insensitive
                            if (roleAar == null)
                            {
                                roleAar = settings.AssignableRoles
                                    .Select(x => (Aar: x, Role: channel.Guild.GetRole(x.RoleId)))
                                    .FirstOrDefault(x => x.Role != null && string.Compare(x.Role.Name, msgContent, true, GlobalDefinitions.Culture) == 0)
                                    .Aar;
                            }

                            if (roleAar == null)
                            {
                                var response = await Communicator.CommandReplyError(message.Channel, "This is not a self-assignable role.");
                                if (settings.ClearRoleChannel)
                                    response.First().DeleteAfter(3);

                                return;
                            }

                            // Check group settings
                            if (!remove && roleAar.Groups.Any())
                            {
                                var existingAssignableRoles = settings.AssignableRoles
                                    .Where(x => user.RoleIds.Contains(x.RoleId) || user.RoleIds.Contains(x.SecondaryId))
                                    .ToList();

                                foreach (var existingAssignableRole in existingAssignableRoles)
                                {
                                    foreach (var commonGroup in existingAssignableRole.Groups.Intersect(roleAar.Groups))
                                    {
                                        if (!settings.GroupSettings.TryGetValue(commonGroup, out var groupSetting))
                                            continue;

                                        if (groupSetting.Limit > 0)
                                        {
                                            var groupRoleCount = existingAssignableRoles.Count(x => x.Groups.Contains(commonGroup));
                                            if (groupRoleCount >= groupSetting.Limit)
                                            {
                                                var response = await Communicator.CommandReplyError(message.Channel, $"You can't add any more roles from group `{commonGroup}`.");
                                                if (settings.ClearRoleChannel)
                                                    response.First().DeleteAfter(3);

                                                return;
                                            }
                                        }
                                    }

                                }
                            }

                            var addRoles = new List<ulong>();
                            var removeRoles = new List<ulong>();
                            if (roleAar.SecondaryId != 0)
                            {
                                //Bias role (more complex logic)
                                if (remove)
                                {
                                    //Remove also secondary
                                    removeRoles.Add(roleAar.RoleId);
                                    removeRoles.Add(roleAar.SecondaryId);
                                }
                                else
                                {
                                    var primaryRoles = settings.AssignableRoles.Where(x => x.SecondaryId != 0);

                                    //If the user doesn't have the primary already
                                    if (!user.RoleIds.Any(x => x == roleAar.RoleId))
                                    {
                                        //Check if user has any primary role
                                        if (user.RoleIds.Any(x => primaryRoles.Any(y => y.RoleId == x)))
                                        {
                                            //Assign secondary
                                            addRoles.Add(roleAar.SecondaryId);
                                        }
                                        else
                                        {
                                            //Assign primary and delete secondary
                                            addRoles.Add(roleAar.RoleId);
                                            removeRoles.Add(roleAar.SecondaryId);
                                        }
                                    }
                                    else
                                        removeRoles.Add(roleAar.SecondaryId); //Try to remove secondary just in case (cleanup)
                                }
                            }
                            else
                            {
                                //Regular role
                                if (remove)
                                    removeRoles.Add(roleAar.RoleId);
                                else
                                    addRoles.Add(roleAar.RoleId);
                            }

                            try
                            {
                                if (addRoles.Count > 0)
                                    await user.AddRolesAsync(addRoles.Select(x => channel.Guild.GetRole(x)).Where(x => x != null));

                                if (removeRoles.Count > 0)
                                    await user.RemoveRolesAsync(removeRoles.Select(x => channel.Guild.GetRole(x)).Where(x => x != null));

                                var guildRole = channel.Guild.GetRole(roleAar.RoleId);
                                if (guildRole != null)
                                {
                                    var response = await Communicator.SendMessage(message.Channel, string.Format(remove ? "You no longer have the **{0}** role." : "You now have the **{0}** role.", guildRole.Name));
                                    if (settings.ClearRoleChannel)
                                        response.First().DeleteAfter(3);
                                }
                            }
                            catch (Discord.Net.HttpException ex) when (ex.HttpCode == HttpStatusCode.Unauthorized || ex.HttpCode == HttpStatusCode.Forbidden)
                            {
                                await Communicator.CommandReplyError(message.Channel, "The bot doesn't have the necessary permissions. If you're the admin, please make sure the bot can Manage Roles and all the assignable roles are placed below the bot's highest role.");
                            }
                        }
                        catch (Exception ex)
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Error, "Roles", "Failed to assign roles", ex));
                            await Communicator.CommandReplyGenericFailure(message.Channel);
                        }
                        finally
                        {
                            if (settings.ClearRoleChannel)
                                message.DeleteAfter(3);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Roles", "Failed to process message", ex));
                }
            });

            return Task.CompletedTask;
        }
    }
}
