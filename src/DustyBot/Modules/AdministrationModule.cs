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
using DustyBot.Settings;

namespace DustyBot.Modules
{
    [Module("Administration", "Commands to help with server admin tasks.")]
    class AdministrationModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }

        public AdministrationModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }
        
        [Command("assignToAll", "Assigns a role to everyone.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}assignToAll RoleName\n\nMay take a while to complete.")]
        public async Task AssignToAll(ICommand command)
        {
            var role = command.Guild.Roles.FirstOrDefault(x => x.Name == (string)command.GetParameter(0));
            if (role == null)
            {
                await command.ReplyError(Communicator, "Role not found.").ConfigureAwait(false);
                return;
            }
            
            await Task.Run(async () =>
            {
                var waitMsg = await command.Reply(Communicator, $"This may take a while...").ConfigureAwait(false);

                var users = await command.Guild.GetUsersAsync().ConfigureAwait(false);
                foreach (var user in users)
                {
                    try
                    {
                        await user.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {

                    }
                }

                await waitMsg.First().DeleteAsync().ConfigureAwait(false);
            });

            await command.ReplySuccess(Communicator, $"Role has been assigned to all users.").ConfigureAwait(false);
        }

        [Command("notInRole", "Checks for users who are missing a specified role.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}notInRole RoleName")]
        public async Task NotInRole(ICommand command)
        {
            var role = command.Guild.Roles.FirstOrDefault(x => x.Name == (string)command.GetParameter(0));
            if (role == null)
            {
                await command.ReplyError(Communicator, "Role not found.").ConfigureAwait(false);
                return;
            }

            string result = "";
            await Task.Run(async () =>
            {
                var users = await command.Guild.GetUsersAsync();
                foreach (var user in users)
                {
                    if (user.RoleIds.Contains(role.Id))
                        continue;

                    result += user.Username + ", ";
                }
            }).ConfigureAwait(false);

            if (result.Length > 2)
                result = result.Substring(0, result.Length - 2);

            if (string.IsNullOrEmpty(result))
                result = "Everyone has this role.";

            await command.Reply(Communicator, result).ConfigureAwait(false);
        }

        [Command("dumpSettings", "Dumps all settings for this server.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}dumpSettings [ServerId]\n\nServerId - bot owner only")]
        public async Task DumpSettings(ICommand command)
        {
            var serverId = command.GuildId;
            var result = await Settings.DumpSettings(serverId);
            
            //TODO - owner option

            await command.Reply(Communicator, result, x => $"```{x}```", 6).ConfigureAwait(false);
        }
    }
}
