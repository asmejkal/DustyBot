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

namespace DustyBot.Modules
{
    [Module("Roles", "Automatic role assignment.")]
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

        [Command("roles", "channel", "Sets or disables a channel for role self-assignment.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Optional)]
        [Comment("Use without parameters to disable role self-assignment.")]
        public async Task SetRoleChannel(ICommand command)
        {
            if (!command[0].HasValue)
            {
                await Settings.Modify(command.GuildId, (RolesSettings s) =>
                {
                    s.RoleChannel = 0;
                }).ConfigureAwait(false);
                
                await command.ReplySuccess(Communicator, "Role channel has been disabled.").ConfigureAwait(false);
            }
            else
            {
                if ((await Settings.Read<RolesSettings>(command.GuildId)).ClearRoleChannel &&
                    (await command.Guild.GetCurrentUserAsync()).GetPermissions(command[0].AsTextChannel).ManageMessages == false)
                {
                    await command.ReplyError(Communicator, $"Automatic message clearing is enabled, but the bot does not have the ManageMessages permission for this channel.");
                    return;
                }

                await Settings.Modify(command.GuildId, (RolesSettings s) =>
                {
                    s.RoleChannel = command[0].AsTextChannel.Id;
                }).ConfigureAwait(false);
                
                await command.ReplySuccess(Communicator, "Role channel has been set.").ConfigureAwait(false);
            }
        }

        [Command("roles", "clearing", "Toggles automatic clearing of role channel.")]
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
                    await command.ReplyError(Communicator, $"To enable automatic clearing, the bot needs ManageMessages permission for the role channel ({channel.Name}).");
                    return;
                }
            }

            bool result = await Settings.Modify(command.GuildId, (RolesSettings s) => s.ClearRoleChannel = !s.ClearRoleChannel).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, $"Automatic role channel clearing has been " + (result ? "enabled" : "disabled") + ".").ConfigureAwait(false);
        }

        [Command("roles", "add", "Adds a self-assignable role.")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("RoleNameOrID", ParameterType.Role, "A name or ID of the self-assignable role.")]
        [Parameter("Alias", ParameterType.String, ParameterFlags.Optional, "A custom alias that can be used to assign this role.")]
        [Parameter("MoreAliases", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder)] //TODO
        [Comment("Any user can then assign this role to themselves by typing its name or alias in the channel set by the `roles channel` command. The role can be also self-removed by typing `-` followed by its name or alias (eg. `-Solar`).")]
        [Example("Solar \"Kim Yongsun\" Yeba")]
        public async Task AddAutoRole(ICommand command)
        {
            var newRole = new AssignableRole();
            newRole.RoleId = command[0].AsRole.Id;
            newRole.Names.Add(command[0].AsRole.Name);
            newRole.Names.AddRange(command.GetParameters().Skip(1).Select(x => (string)x));

            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                s.AssignableRoles.Add(newRole);
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, $"Self-assignable role added ({newRole.RoleId}).");
        }

        [Command("roles", "remove", "Removes a self-assignable role.")]
        [Permissions(GuildPermission.ManageRoles)]
        [Parameter("RoleNameOrID", ParameterType.Role)]
        public async Task RemoveAutoRole(ICommand command)
        {
            bool removed = await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                return s.AssignableRoles.RemoveAll(x => x.RoleId == command[0].AsRole.Id) > 0;
            }).ConfigureAwait(false);

            if (!removed)
                await command.ReplySuccess(Communicator, $"This role is not self-assignable.").ConfigureAwait(false);
            else
                await command.ReplySuccess(Communicator, $"Self-assignable role removed.").ConfigureAwait(false);
        }

        [Command("roles", "clear", "Removes all self-assignable roles.")]
        [Permissions(GuildPermission.ManageRoles)]
        public async Task ClearAutoRoles(ICommand command)
        {
            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                s.AssignableRoles.Clear();
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, $"All self-assignable roles have been cleared.").ConfigureAwait(false);
        }

        [Command("roles", "list", "Lists all self-assignable roles.")]
        [Permissions(GuildPermission.ManageRoles)]
        public async Task ListAutoRoles(ICommand command)
        {
            var settings = await Settings.Read<RolesSettings>(command.GuildId).ConfigureAwait(false);

            var result = string.Empty;
            foreach (var role in settings.AssignableRoles)
            {
                result += $"\nName: `{role.Names.FirstOrDefault() ?? string.Empty}` ";

                if (role.Names.Count > 1)
                {
                    result += $"Aliases: `" + string.Join(", ", role.Names.Skip(1)) + "` ";
                }

                if (role.SecondaryId != 0)
                {
                    result += $"Secondary: `";
                    try
                    {
                        var secondary = command.Guild.Roles.First(x => x.Id == role.SecondaryId);
                        result += secondary.Name;
                    }
                    catch (InvalidOperationException)
                    {
                        result += "INVALID";
                    }
                    result += "` ";
                }
            }

            if (string.IsNullOrEmpty(result))
                result = "No self-assignable roles have been setup.";

            await command.Reply(Communicator, result).ConfigureAwait(false);
        }

        [Command("roles", "setbias", "Sets a primary-secondary bias role pair.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("PrimaryRoleNameOrID", ParameterType.Role)]
        [Parameter("SecondaryRoleNameOrID", ParameterType.Role)]
        [Comment("If a user already has **any** primary role assigned, then the bot will assign this secondary role instead. This means that the first bias role a user sets will be their primary. After that, any other bias role they assign will become secondary. They may change their primary bias by removing the primary bias and assigning a new one.\n\nIf you run:\n`{p}roles add Solar`\n`{p}roles add Wheein`\n`{p}roles setbias Solar .Solar`\n`{p}roles setbias Wheein .Wheein`\n\nThen typing this in the role channel:\n`Solar`\n`Wheein`\n\nWill result in the user having a primary `Solar` role and a secondary `.Wheein` role.")]
        [Example("Solar .Solar")]
        public async Task SetBiasRole(ICommand command)
        {
            bool notAar = false;
            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                var primaryAar = s.AssignableRoles.FirstOrDefault(x => x.RoleId == command[0].AsRole.Id);
                if (primaryAar == null)
                    notAar = true;
                else
                    primaryAar.SecondaryId = command[1].AsRole.Id;
            }).ConfigureAwait(false);

            if (notAar)
            {
                await command.ReplyError(Communicator, $"The primary role is not self-assignable.").ConfigureAwait(false);
            }
            else
            {
                await command.ReplySuccess(Communicator, $"Role {command[0].AsRole.Name} ({command[0].AsRole.Id}) has been set as a primary bias role to {command[1].AsRole.Name} ({command[1].AsRole.Id}).").ConfigureAwait(false);
            }
        }

        [Command("autorole", "add", "Assign a role automatically upon joining.")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("RoleNameOrID", ParameterType.Role)]
        public async Task AutoRoleAdd(ICommand command)
        {
            await Settings.Modify(command.GuildId, (RolesSettings s) => s.AutoAssignRoles.Add(command[0].AsRole.Id)).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, $"Will now assign role {command[0].AsRole.Name} ({command[0].AsRole.Id}) to users upon joining.");
        }

        [Command("autorole", "remove", "Remove an automatically assigned role.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("RoleNameOrID", ParameterType.Role)]
        public async Task AutoRoleRemove(ICommand command)
        {
            var removed = await Settings.Modify(command.GuildId, (RolesSettings s) => s.AutoAssignRoles.Remove(command[0].AsRole.Id)).ConfigureAwait(false);

            if (removed)
                await command.ReplySuccess(Communicator, $"Will no longer assign role {command[0].AsRole.Name} ({command[0].AsRole.Id}).");
            else
                await command.ReplyError(Communicator, $"This role is not being assigned automatically.");
        }

        public override async Task OnMessageReceived(SocketMessage message)
        {
            try
            {
                var channel = message.Channel as ITextChannel;
                if (channel == null)
                    return;

                var settings = await Settings.Read<RolesSettings>(channel.GuildId).ConfigureAwait(false);
                if (channel.Id != settings.RoleChannel)
                    return;

                var user = message.Author as IGuildUser;
                if (user == null)
                    return;

                if (user.IsBot)
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

                    AssignableRole roleAar = null;
                    foreach (var assignableRole in settings.AssignableRoles)
                    {
                        if (assignableRole.Names.FirstOrDefault(x => x.Equals(msgContent, StringComparison.CurrentCultureIgnoreCase)) != null)
                            roleAar = assignableRole;
                    }

                    if (roleAar == null)
                    {
                        var response = await Communicator.CommandReplyError(message.Channel, "A self-assignable role name expected.").ConfigureAwait(false);
                        if (settings.ClearRoleChannel)
                            response.DeleteAfter(3);

                        return;
                    }

                    List<ulong> addRoles = new List<ulong>();
                    List<ulong> removeRoles = new List<ulong>();

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

                    {
                        var response = await Communicator.SendMessage(message.Channel, string.Format(remove ? "You no longer have the **{0}** role." : "You now have the **{0}** role.", roleAar.Names.FirstOrDefault())).ConfigureAwait(false);
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

        public override Task OnUserJoined(SocketGuildUser guildUser)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var settings = await Settings.Read<RolesSettings>(guildUser.Guild.Id, false);
                    if (settings == null)
                        return;

                    if (settings.AutoAssignRoles.Count <= 0)
                        return;
                    
                    var roles = guildUser.Guild.Roles.Where(x => settings.AutoAssignRoles.Contains(x.Id)).ToList();
                    if (roles.Count <= 0)
                        return;

                    await Logger.Log(new LogMessage(LogSeverity.Info, "Roles", $"Auto-assigning {roles.Count} role(s) to {guildUser.Username} ({guildUser.Id}) on {guildUser.Guild.Name}"));

                    await guildUser.AddRolesAsync(roles);
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Events", "Failed to process greeting event", ex));
                }
            });

            return Task.CompletedTask;
        }
    }
}
