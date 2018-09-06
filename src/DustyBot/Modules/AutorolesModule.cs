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
    [Module("Autoroles", "Give roles to all members automatically.")]
    class AutorolesModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public ILogger Logger { get; private set; }

        public AutorolesModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
        }
        
        [Command("autorole", "add", "Assign a role automatically upon joining.")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("RoleNameOrID", ParameterType.Role, ParameterFlags.Remainder)]
        public async Task AddAutoRole(ICommand command)
        {
            await Settings.Modify(command.GuildId, (RolesSettings s) => s.AutoAssignRoles.Add(command[0].AsRole.Id)).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, $"Will now assign role {command[0].AsRole.Name} ({command[0].AsRole.Id}) to users upon joining.");
        }

        [Command("autorole", "remove", "Remove an automatically assigned role.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("RoleNameOrID", ParameterType.Role, ParameterFlags.Remainder)]
        public async Task RemoveAutoRole(ICommand command)
        {
            var removed = await Settings.Modify(command.GuildId, (RolesSettings s) => s.AutoAssignRoles.Remove(command[0].AsRole.Id)).ConfigureAwait(false);

            if (removed)
                await command.ReplySuccess(Communicator, $"Will no longer assign role {command[0].AsRole.Name} ({command[0].AsRole.Id}).");
            else
                await command.ReplyError(Communicator, $"This role is not being assigned automatically.");
        }

        [Command("autorole", "list", "Lists all automatically assigned roles.")]
        [Permissions(GuildPermission.ManageRoles)]
        public async Task ListAutoRole(ICommand command)
        {
            var settings = await Settings.Read<RolesSettings>(command.GuildId).ConfigureAwait(false);
            if (settings.AutoAssignRoles.Count <= 0)
            {
                await command.ReplyError(Communicator, "No automatically assigned roles have been set.");
                return;
            }

            var result = string.Empty;
            foreach (var role in settings.AutoAssignRoles)
            {
                result += $"\nId: `{role}` Name: `{command.Guild.Roles.FirstOrDefault(x => x.Id == role)?.Name ?? "DOES NOT EXIST"}`";
            }

            await command.Reply(Communicator, result);
        }

        [Command("autorole", "apply", "Assigns the current automatic roles to everyone.", CommandFlags.RunAsync)]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Comment("May take a while to complete.")]
        public async Task ApplyAutoRole(ICommand command)
        {
            var settings = await Settings.Read<RolesSettings>(command.GuildId).ConfigureAwait(false);
            if (settings.AutoAssignRoles.Count <= 0)
            {
                await command.ReplyError(Communicator, "No automatically assigned roles have been set.");
                return;
            }

            var waitMsg = await command.Reply(Communicator, $"This may take a while...").ConfigureAwait(false);

            var failed = 0;
            var roles = command.Guild.Roles.Where(x => settings.AutoAssignRoles.Any(y => x.Id == y)).ToList();
            var users = await command.Guild.GetUsersAsync().ConfigureAwait(false);
            foreach (var user in users)
            {
                try
                {
                    await user.AddRolesAsync(roles).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    failed++;
                }
            }

            await waitMsg.First().DeleteAsync().ConfigureAwait(false);
            await command.ReplySuccess(Communicator, $"Roles have been assigned to all users" + (failed > 0 ? $" ({failed} failed)." : ".")).ConfigureAwait(false);
        }

        [Command("autorole", "check", "Checks for users who are missing an automatic role.", CommandFlags.RunAsync)]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        public async Task CheckAutoRole(ICommand command)
        {
            string result = string.Empty;
            var settings = await Settings.Read<RolesSettings>(command.GuildId).ConfigureAwait(false);
            if (settings.AutoAssignRoles.Count <= 0)
            {
                await command.ReplyError(Communicator, "No automatically assigned roles have been set.");
                return;
            }

            var users = await command.Guild.GetUsersAsync();
            foreach (var user in users)
            {
                if (settings.AutoAssignRoles.All(x => user.RoleIds.Contains(x)))
                    continue;

                result += user.Username + "#" + user.Discriminator + ", ";
            }

            if (result.Length > 2)
                result = result.Substring(0, result.Length - 2);

            if (string.IsNullOrEmpty(result))
                result = settings.AutoAssignRoles.Count > 1 ? "Everyone has these roles." : "Everyone has this role.";

            await command.Reply(Communicator, result).ConfigureAwait(false);
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
