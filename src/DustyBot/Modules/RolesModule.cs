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

        [Command("setRoleChannel", "Sets or disables a channel for role self-assignment.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}setRoleChannel ChannelMention\n\nUse without parameters to disable role self-assignment.")]
        public async Task SetRoleChannel(ICommand command)
        {
            if (command.Message.MentionedChannelIds.Count <= 0)
            {
                await Settings.Modify(command.GuildId, (RolesSettings s) =>
                {
                    s.RoleChannel = 0;
                }).ConfigureAwait(false);
                
                await command.ReplySuccess(Communicator, "Role channel has been disabled.").ConfigureAwait(false);
            }
            else
            {
                await Settings.Modify(command.GuildId, (RolesSettings s) =>
                {
                    s.RoleChannel = command.Message.MentionedChannelIds.First();
                }).ConfigureAwait(false);
                
                await command.ReplySuccess(Communicator, "Role channel has been set.").ConfigureAwait(false);
            }
        }

        [Command("setRoleChannelClearing", "Toggles automatic clearing of role channel.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}setRoleChannelClearing\n\nDisabled by default.")]
        public async Task SetRoleChannelClearing(ICommand command)
        {
            bool result = false;
            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                s.ClearRoleChannel = result = !s.ClearRoleChannel;
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, $"Automatic role channel clearing has been " + (result ? "enabled" : "disabled") + ".").ConfigureAwait(false);
        }

        [Command("addAutoRole", "Adds a self-assignable role.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}addAutoRole \"RoleName\" [\"Aliases\"]\n\nExample: {p}addAutoRole Solar \"Kim Yongsun\" Yeba")]
        public async Task AddAutoRole(ICommand command)
        {
            var role = command.Guild.Roles.FirstOrDefault(x => string.Equals(x.Name, (string)command.GetParameter(0), StringComparison.CurrentCultureIgnoreCase));
            if (role == null)
            {
                await command.ReplyError(Communicator, $"Role {(string)command.GetParameter(0)} not found.").ConfigureAwait(false);
                return;
            }

            var newRole = new Settings.AssignableRole();
            newRole.RoleId = role.Id;
            newRole.Names = new List<string>(command.GetParameters().Select(x => (string)x));

            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                s.AssignableRoles.Add(newRole);
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, $"Self-assignable role added ({newRole.RoleId}).");
        }

        [Command("removeAutoRole", "Removes a self-assignable role.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}removeAutoRole RoleName")]
        public async Task RemoveAutoRole(ICommand command)
        {
            bool removed = await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                return s.AssignableRoles.RemoveAll(x => string.Equals(x.Names.First(), command.Body)) > 0;
            }).ConfigureAwait(false);

            if (!removed)
            {
                await command.ReplySuccess(Communicator, $"Self-assignable role not found.").ConfigureAwait(false);
            }
            else
            {
                await command.ReplySuccess(Communicator, $"Self-assignable role removed.").ConfigureAwait(false);
            }
        }

        [Command("clearAutoRoles", "Removes all self-assignable roles.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}clearAutoRoles")]
        public async Task ClearAutoRoles(ICommand command)
        {
            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                s.AssignableRoles.Clear();
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, $"All self-assignable roles have been cleared.").ConfigureAwait(false);
        }

        [Command("listAutoRoles", "Lists all self-assignable roles.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}listAutoRoles")]
        public async Task ListAutoRoles(ICommand command)
        {
            var settings = await Settings.Read<RolesSettings>(command.GuildId).ConfigureAwait(false);

            string result = "```";
            foreach (var role in settings.AssignableRoles)
            {
                result += role.Names.FirstOrDefault();

                if (role.Names.Count > 1 || role.SecondaryId != 0)
                {
                    result += " (";

                    //Aliases
                    result += String.Join(", ", role.Names.Skip(1));

                    //Secondary
                    if (role.SecondaryId != 0)
                    {
                        result += (role.Names.Count > 1 ? " | " : "") + "secondary: ";
                        try
                        {
                            var secondary = command.Guild.Roles.First(x => x.Id == role.SecondaryId);
                            result += secondary.Name;
                        }
                        catch (InvalidOperationException)
                        {
                            result += "INVALID";
                        }
                    }

                    result += ")";
                }

                result += ", ";
            }

            if (result.Length > 5)
                result = result.Substring(0, result.Length - 2);

            if (result.Length <= 3) //Empty
                result = "None";
            else
                result += "```";

            var embed = new EmbedBuilder().WithTitle("Self-assignable roles").WithDescription(result);

            await command.Message.Channel.SendMessageAsync("", false, embed).ConfigureAwait(false);
        }

        [Command("setBiasRole", "Sets a primary-secondary bias role pair.")]
        [Parameters(ParameterType.String, ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}setBiasRole \"PrimaryRoleName\" \"SecondaryRoleName\"\n\nExample: {p}setBiasRole Solar .Solar")]
        public async Task SetBiasRole(ICommand command)
        {
            var primary = command.Guild.Roles.FirstOrDefault(x => string.Equals(x.Name, (string)command.GetParameter(0), StringComparison.CurrentCultureIgnoreCase));
            var secondary = command.Guild.Roles.FirstOrDefault(x => string.Equals(x.Name, (string)command.GetParameter(1), StringComparison.CurrentCultureIgnoreCase));
            if (primary == null || secondary == null)
            {
                await command.ReplyError(Communicator, $"Role not found.").ConfigureAwait(false);
                return;
            }

            bool notAar = false;
            await Settings.Modify(command.GuildId, (RolesSettings s) =>
            {
                var primaryAar = s.AssignableRoles.FirstOrDefault(x => x.RoleId == primary.Id);
                if (primaryAar == null)
                    notAar = true;
                else
                    primaryAar.SecondaryId = secondary.Id;
            }).ConfigureAwait(false);

            if (notAar)
            {
                await command.ReplyError(Communicator, $"The primary role is not self-assignable.").ConfigureAwait(false);
            }
            else
            {
                await command.ReplySuccess(Communicator, $"Role {primary.Name} ({primary.Id}) has been set as a primary bias role to {secondary.Name} ({secondary.Id}).").ConfigureAwait(false);
            }
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
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Message", "\" " + message.Content + "\" by " + message.Author.Username + " (" + message.Author.Id + ")"));

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

                    //await System.Console.Out.WriteLineAsync(DateTime.Now.ToString("HH:mm:ss") + " Message     \"" + message.Content + "\" by " + message.Author.Username + " (" + message.Author.Id + ")");

                    if (roleAar == null)
                    {
                        var response = await message.Channel.SendMessageAsync("A self-assignable role name expected.").ConfigureAwait(false);
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
                        var response = await message.Channel.SendMessageAsync(String.Format(remove ? "You no longer have the **{0}** role." : "You now have the **{0}** role.", roleAar.Names.FirstOrDefault())).ConfigureAwait(false);
                        if (settings.ClearRoleChannel)
                            response.DeleteAfter(3);
                    }
                }
                finally
                {
                    if (settings.ClearRoleChannel)
                        message.DeleteAfter(3);
                }
            }
            catch (Exception)
            {
                //Log
            }
        }
    }
}
