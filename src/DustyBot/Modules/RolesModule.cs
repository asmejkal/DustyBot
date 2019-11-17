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
using DustyBot.Framework.Logging;
using DustyBot.Settings;
using Discord.WebSocket;
using DustyBot.Helpers;
using DustyBot.Definitions;

namespace DustyBot.Modules
{
    [Module("Roles", "Role self-assignment.")]
    class RolesModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public ILogger Logger { get; private set; }

        public RolesModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger)
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
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
        }

        [Command("roles", "channel", "Sets or disables a channel for role self-assignment.")]
        [Alias("role", "channel")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Optional)]
        [Comment("Use without parameters to disable role self-assignment.")]
        public async Task SetRoleChannel(ICommand command)
        {
            if (!command["Channel"].HasValue)
            {
                await Settings.Modify(command.GuildId, (RolesSettings s) =>
                {
                    s.RoleChannel = 0;
                }).ConfigureAwait(false);
                
                await command.ReplySuccess(Communicator, "Role channel has been disabled.").ConfigureAwait(false);
            }
            else
            {
                var permissions = (await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel);
                if (!permissions.SendMessages)
                {
                    await command.ReplyError(Communicator, $"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                    return;
                }

                if ((await Settings.Read<RolesSettings>(command.GuildId)).ClearRoleChannel && !permissions.ManageMessages)
                {
                    await command.ReplyError(Communicator, $"Automatic message clearing is enabled, but the bot does not have the ManageMessages permission for this channel.");
                    return;
                }

                await Settings.Modify(command.GuildId, (RolesSettings s) =>
                {
                    s.RoleChannel = command["Channel"].AsTextChannel.Id;
                }).ConfigureAwait(false);
                
                await command.ReplySuccess(Communicator, "Role channel has been set.").ConfigureAwait(false);
            }
        }

        [Command("roles", "clearing", "Toggles automatic clearing of role channel.")]
        [Alias("role", "clearing")]
        [Permissions(GuildPermission.ManageMessages)]
        [Comment("Disabled by default.")]
        public async Task SetRoleChannelClearing(ICommand command)
        {
            var settings = await Settings.Read<RolesSettings>(command.GuildId);
            if (!settings.ClearRoleChannel == true && settings.RoleChannel != 0)
            {
                var channel = await command.Guild.GetChannelAsync(settings.RoleChannel);
                if (channel != null && (await channel.Guild.GetCurrentUserAsync()).GetPermissions(channel).ManageMessages == false)
                {
                    await command.ReplyError(Communicator, $"To enable automatic clearing, the bot needs the ManageMessages permission for the role channel ({channel.Name}).");
                    return;
                }
            }

            bool result = await Settings.Modify(command.GuildId, (RolesSettings s) => s.ClearRoleChannel = !s.ClearRoleChannel).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, $"Automatic role channel clearing has been " + (result ? "enabled" : "disabled") + ".").ConfigureAwait(false);
        }

        [Command("roles", "create", "Creates a new self-assignable role.")]
        [Alias("role", "create")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("Name", ParameterType.String, ParameterFlags.Remainder, "what to name the role which will be created")]
        [Comment("Any user can then assign this role to themselves by typing its name or alias (without any prefix) in the channel set by the `roles channel` command. The role can be also self-removed by typing `-` followed by its name or alias (eg. `-Solar`).")]
        [Example("Solar")]
        public async Task CreateRole(ICommand command)
        {
            var role = await command.Guild.CreateRoleAsync(command["Name"], new GuildPermissions());
            await AddRole(command.GuildId, role, command.Channel);

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
            var botMaxPosition = (await command.Guild.GetCurrentUserAsync()).RoleIds.Select(x => command.Guild.GetRole(x)).Max(x => x?.Position ?? 0);
            if (command["RoleNameOrID"].AsRole.Position >= botMaxPosition)
            {
                await command.ReplyError(Communicator, $"The bot doesn't have permission to assign this role to users. Please make sure the role is below the bot's highest role in the server's role list.");
                return;
            }
            
            await AddRole(command.GuildId, command["RoleNameOrID"].AsRole, command.Channel);

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
            }).ConfigureAwait(false);

            if (!removed)
                await command.ReplyError(Communicator, $"This role is not self-assignable.").ConfigureAwait(false);
            else
                await command.ReplySuccess(Communicator, $"Self-assignable role removed.").ConfigureAwait(false);
        }

        [Command("roles", "list", "Lists all self-assignable roles.")]
        [Alias("role", "list")]
        [Permissions(GuildPermission.ManageRoles)]
        public async Task ListAutoRoles(ICommand command)
        {
            var settings = await Settings.Read<RolesSettings>(command.GuildId).ConfigureAwait(false);

            var result = new StringBuilder();
            foreach (var role in settings.AssignableRoles)
            {
                var guildRole = command.Guild.Roles.FirstOrDefault(x => x.Id == role.RoleId);
                result.Append($"\nRole: `{(guildRole?.Name ?? "DELETED ROLE")}` ");

                if (role.Names.Any())
                    result.Append($"Aliases: `" + string.Join(", ", role.Names) + "` ");

                if (role.SecondaryId != 0)
                    result.Append($"Secondary: `{command.Guild.Roles.FirstOrDefault(x => x.Id == role.SecondaryId)?.Name ?? "DELETED ROLE"}` ");
            }

            if (result.Length <= 0)
                result.Append("No self-assignable roles have been set up.");

            await command.Reply(Communicator, result.ToString()).ConfigureAwait(false);
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
                    throw new Framework.Exceptions.IncorrectParametersCommandException($"Role `{command["RoleNameOrId"].AsRole.Id}` is not self-assignable.");

                var roleNames = s.AssignableRoles
                    .Select(x => command.Guild.GetRole(x.RoleId)?.Name)
                    .Where(x => x != null)
                    .Concat(s.AssignableRoles.SelectMany(x => x.Names));

                if (roleNames.Contains(command["Alias"]))
                    throw new Framework.Exceptions.CommandException("A self-assignable role with this name or alias already exists");

                role.Names.Add(command["Alias"]);
            });

            await command.ReplySuccess(Communicator, $"Alias `{command["Alias"]}` added.").ConfigureAwait(false);
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
                    throw new Framework.Exceptions.IncorrectParametersCommandException($"Role `{command["RoleNameOrId"].AsRole.Id}` is not self-assignable.");

                return role.Names.RemoveAll(x => x == command["Alias"]);
            }).ConfigureAwait(false);

            if (removed <= 0)
                await command.ReplyError(Communicator, $"No alias found with this name for role `{command["RoleNameOrId"].AsRole.Id}`.").ConfigureAwait(false);
            else
                await command.ReplySuccess(Communicator, $"Alias `{command["Alias"]}` removed.").ConfigureAwait(false);
        }

        [Command("roles", "setbias", "Sets a primary-secondary bias role pair.")]
        [Alias("role", "setbias")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("PrimaryRoleNameOrID", ParameterType.Role)]
        [Parameter("SecondaryRoleNameOrID", ParameterType.Role)]
        [Comment("Marks a role as a primary bias role and links it with a secondary role. If a user already has **any** primary bias role assigned, then the bot will assign this secondary role instead. This means that the first bias role a user sets will be their primary. After that, any other bias role they assign will become secondary. They may change their primary bias by removing the primary bias and assigning a new one.\n\nIf you run:\n`{p}roles add Solar`\n`{p}roles add Wheein`\n`{p}roles setbias Solar .Solar`\n`{p}roles setbias Wheein .Wheein`\n\nThen typing this in the role channel:\n`Solar`\n`Wheein`\n\nWill result in the user having a primary `Solar` role and a secondary `.Wheein` role.")]
        [Example("Solar .Solar")]
        public async Task SetBiasRole(ICommand command)
        {
            if (command["PrimaryRoleNameOrID"].AsRole.Id == command["SecondaryRoleNameOrID"].AsRole.Id)
            {
                await command.ReplyError(Communicator, $"The primary and secondary roles can't be the same role.").ConfigureAwait(false);
                return;
            }

            var assignable = await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                var primaryAar = s.AssignableRoles.FirstOrDefault(x => x.RoleId == command["PrimaryRoleNameOrID"].AsRole.Id);
                if (primaryAar != null)
                    primaryAar.SecondaryId = command["SecondaryRoleNameOrID"].AsRole.Id;

                return primaryAar != null;
            }).ConfigureAwait(false);

            if (assignable)
                await command.ReplySuccess(Communicator, $"Role `{command[0].AsRole.Name} ({command[0].AsRole.Id})` has been set as a primary bias role to `{command[1].AsRole.Name} ({command[1].AsRole.Id})`.").ConfigureAwait(false);
            else
                await command.ReplyError(Communicator, $"The primary role is not self-assignable.").ConfigureAwait(false);
        }

        [Command("roles", "persistence", "Restore self-assignable roles upon rejoining the server.")]
        [Alias("role", "persistence")]
        [Permissions(GuildPermission.Administrator)]
        [Comment("Toggle. All self-assignable roles the user had upon leaving will be reapplied if they rejoin. The feature had to be turned on when the user left for it to function.")]
        public async Task PersistentRoles(ICommand command)
        {
            var selfUser = await command.Guild.GetCurrentUserAsync();
            var newVal = await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                if (!s.PersistentAssignableRoles && selfUser.GuildPermissions.ManageRoles == false)
                    throw new Framework.Exceptions.MissingBotPermissionsException(GuildPermission.ManageRoles);

                return s.PersistentAssignableRoles = !s.PersistentAssignableRoles;
            }).ConfigureAwait(false);
                        
            await command.ReplySuccess(Communicator, $"Self-assignable roles will {(newVal ? "now" : "no longer")} be restored for users who leave and rejoin the server.").ConfigureAwait(false);
        }

        [Command("roles", "stats", "Server roles statistics.", CommandFlags.RunAsync)]
        [Alias("role", "stats")]
        [Parameter("all", "all", ParameterFlags.Optional, "prints stats for all roles")]
        public async Task RolesStats(ICommand command)
        {
            var data = new Dictionary<ulong, int>();
            foreach (var role in command.Guild.Roles)
                data[role.Id] = 0;

            foreach (var user in await command.Guild.GetUsersAsync())
                foreach (var role in user.RoleIds)
                    data[role] += 1;

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

                    pages.Last.Embed.Description += $"**{command.Guild.Roles.First(x => x.Id == kv.Key).Name}:** {kv.Value} user{(kv.Value != 1 ? "s" : "")}\n";
                }
            }
            else
            {
                var settings = await Settings.Read<RolesSettings>(command.GuildId, false);
                if (settings == null || settings.AssignableRoles.Count <= 0)
                {
                    await command.Reply(Communicator, "No self-assignable roles have been set it. For statistics of all roles use `roles stats all`.").ConfigureAwait(false);
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
                        pages.Add(new EmbedBuilder().WithTitle("Self-assignable roles").WithDescription(string.Empty));

                    pages.Last.Embed.Description += $"**{guildRole.Name}:** {kv.Value} user{(kv.Value != 1 ? "s" : "")}\n";
                    
                    if (role.SecondaryId != default)
                    {
                        pages.Last.Embed.Description += $" ┕ Secondary: {data[role.SecondaryId]} users\n";

                        if (count % MaxLines != 0)
                            count++;
                    }                        
                }
            }

            await command.Reply(Communicator, pages).ConfigureAwait(false);
        }

        private async Task AddRole(ulong guildId, IRole role, IMessageChannel replyChannel)
        {
            var newRole = new AssignableRole();
            newRole.RoleId = role.Id;

            await Settings.Modify(guildId, (RolesSettings s) =>
            {
                if (s.AssignableRoles.Any(x => x.RoleId == role.Id))
                    throw new Framework.Exceptions.CommandException("This role is already self-assignable.");

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

        public override async Task OnMessageReceived(SocketMessage message)
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

                var settings = await Settings.Read<RolesSettings>(channel.GuildId).ConfigureAwait(false);
                if (channel.Id != settings.RoleChannel)
                    return;

                try
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Roles", $"\"{message.Content}\" by {message.Author.Username} ({message.Author.Id}) on {channel.Guild.Name}"));

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
                    var roleAar = settings.AssignableRoles.FirstOrDefault(x => x.Names.Any(y => string.Compare(y, msgContent) == 0));
                    
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
                        roleAar = settings.AssignableRoles.FirstOrDefault(x => x.Names.Any(y => string.Compare(y, msgContent, true, GlobalDefinitions.Culture) == 0));

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
                        var response = await Communicator.CommandReplyError(message.Channel, "This is not a self-assignable role.").ConfigureAwait(false);
                        if (settings.ClearRoleChannel)
                            response.DeleteAfter(3);

                        return;
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


                    if (addRoles.Count > 0)
                        await user.AddRolesAsync(addRoles.Select(x => channel.Guild.GetRole(x)).Where(x => x != null));

                    if (removeRoles.Count > 0)
                        await user.RemoveRolesAsync(removeRoles.Select(x => channel.Guild.GetRole(x)).Where(x => x != null));

                    var guildRole = channel.Guild.GetRole(roleAar.RoleId);
                    if (guildRole != null)
                    {
                        var response = await Communicator.SendMessage(message.Channel, string.Format(remove ? "You no longer have the **{0}** role." : "You now have the **{0}** role.", guildRole.Name)).ConfigureAwait(false);
                        if (settings.ClearRoleChannel)
                            response.First().DeleteAfter(3);
                    }
                }
                finally
                {
                    if (settings.ClearRoleChannel)
                        message.DeleteAfter(3);
                }
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Roles", "Failed to process message", ex));
            }
        }
    }
}
