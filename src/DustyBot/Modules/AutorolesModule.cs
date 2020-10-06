using Discord;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Settings;
using Discord.WebSocket;
using DustyBot.Helpers;
using DustyBot.Framework.Exceptions;
using DustyBot.Database.Services;
using DustyBot.Core.Async;
using DustyBot.Core.Formatting;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Text;

namespace DustyBot.Modules
{
    [Module("Autoroles", "Give roles to all members automatically.")]
    class AutorolesModule : Module
    {
        public ICommunicator Communicator { get; }
        public ISettingsService Settings { get; }
        public ILogger Logger { get; }

        private ConcurrentDictionary<ulong, bool> ApplyTasks = new ConcurrentDictionary<ulong, bool>();
        private ConcurrentDictionary<ulong, bool> CheckTasks = new ConcurrentDictionary<ulong, bool>();

        public AutorolesModule(ICommunicator communicator, ISettingsService settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
        }

        [Command("autorole", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("autoroles", "help"), Alias("autorole"), Alias("autoroles")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
        }

        [Command("autorole", "add", "Assign a role automatically upon joining.")]
        [Alias("autoroles", "add")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("RoleNameOrID", ParameterType.Role, ParameterFlags.Remainder)]
        public async Task AddAutoRole(ICommand command)
        {
            var botMaxPosition = (await command.Guild.GetCurrentUserAsync()).RoleIds.Select(x => command.Guild.GetRole(x)).Max(x => x?.Position ?? 0);
            if (command["RoleNameOrID"].AsRole.Position >= botMaxPosition)
            {
                await command.ReplyError(Communicator, $"The bot doesn't have permission to assign this role to users. Please make sure the role is below the bot's highest role in the server's role list.");
                return;
            }

            await Settings.Modify(command.GuildId, (RolesSettings s) => s.AutoAssignRoles.Add(command[0].AsRole.Id));
            await command.ReplySuccess(Communicator, $"Will now assign role `{command[0].AsRole.Name} ({command[0].AsRole.Id})` to users upon joining.");
        }

        [Command("autorole", "remove", "Remove an automatically assigned role.")]
        [Alias("autoroles", "remove")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("RoleNameOrID", ParameterType.Role, ParameterFlags.Remainder)]
        public async Task RemoveAutoRole(ICommand command)
        {
            var removed = await Settings.Modify(command.GuildId, (RolesSettings s) => s.AutoAssignRoles.Remove(command[0].AsRole.Id));

            if (removed)
                await command.ReplySuccess(Communicator, $"Will no longer assign role `{command[0].AsRole.Name} ({command[0].AsRole.Id})`.");
            else
                await command.ReplyError(Communicator, $"This role is not being assigned automatically.");
        }

        [Command("autorole", "list", "Lists all automatically assigned roles.")]
        [Alias("autoroles", "list")]
        [Permissions(GuildPermission.ManageRoles)]
        public async Task ListAutoRole(ICommand command)
        {
            var settings = await Settings.Read<RolesSettings>(command.GuildId);
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

        [Command("autorole", "apply", "Assigns the current automatic roles to everyone.")]
        [Alias("autoroles", "apply")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Comment("May take a while to complete.")]
        public async Task ApplyAutoRole(ICommand command)
        {
            var settings = await Settings.Read<RolesSettings>(command.GuildId);
            if (settings.AutoAssignRoles.Count <= 0)
            {
                await command.ReplyError(Communicator, "No automatically assigned roles have been set.");
                return;
            }

            var added = ApplyTasks.TryAdd(command.GuildId, true);
            if (!added)
                throw new AbortException("Please wait a while before running this command again.");

            try
            {
                string BuildProgressMessage(int processed, int total) => $"This may take a while... assigning roles to users: {processed}/{total}";

                var guild = (SocketGuild)command.Guild;
                var progressMsg = (await command.Reply(Communicator, BuildProgressMessage(0, guild.MemberCount))).First();
                var roles = command.Guild.Roles.Where(x => settings.AutoAssignRoles.Any(y => x.Id == y)).ToList();

                var processed = 0;
                var failed = 0;
                await foreach (var batch in guild.GetUsersAsync())
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Autoroles", $"Applying autoroles {processed}/{guild.MemberCount} on server {command.Guild.Name} ({command.GuildId})"));
                    foreach (var user in batch)
                    {
                        try
                        {
                            foreach (var role in roles.Where(x => !user.RoleIds.Contains(x.Id)))
                                await user.AddRoleAsync(role);
                        }
                        catch (Discord.Net.HttpException ex) when (ex.HttpCode == HttpStatusCode.Unauthorized || ex.HttpCode == HttpStatusCode.Forbidden)
                        {
                            throw new CommandException("The bot doesn't have the necessary permissions. Please make sure all of the assigned roles are placed below the bot's highest role.");
                        }
                        catch (Exception ex)
                        {
                            if (failed <= 5)
                                await Logger.Log(new LogMessage(LogSeverity.Error, "Autoroles", $"Failed to apply autoroles {roles.Select(x => x.Id).WordJoin()} on server {command.Guild.Name} ({command.GuildId})", ex));

                            failed++;
                        }
                    }

                    processed += batch.Count;
                    await progressMsg.ModifyAsync(x => x.Content = BuildProgressMessage(processed, guild.MemberCount));
                }

                await progressMsg.DeleteAsync();
                await command.ReplySuccess(Communicator, $"Roles have been assigned to all users" + (failed > 0 ? $" ({failed} failed)." : "."));
            }
            finally
            {
                TaskHelper.FireForget(async () =>
                {
                    await Task.Delay(TimeSpan.FromHours(1));
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Autoroles", $"Removing autorole apply command lock for guild {command.GuildId}"));
                    ApplyTasks.TryRemove(command.GuildId, out _);
                });
            }
        }

        [Command("autorole", "check", "Checks for users who are missing an automatic role.")]
        [Alias("autoroles", "check")]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        public async Task CheckAutoRole(ICommand command)
        {
            var settings = await Settings.Read<RolesSettings>(command.GuildId);
            if (settings.AutoAssignRoles.Count <= 0)
            {
                await command.ReplyError(Communicator, "No automatically assigned roles have been set.");
                return;
            }

            var added = CheckTasks.TryAdd(command.GuildId, true);
            if (!added)
                throw new AbortException("Please wait a while before running this command again.");

            try
            {
                string BuildProgressMessage(int processed, int total) => $"This may take a while... checking users: {processed}/{total}";

                var guild = (SocketGuild)command.Guild;
                var progressMsg = (await command.Reply(Communicator, BuildProgressMessage(0, guild.MemberCount))).First();
                var roles = command.Guild.Roles.Where(x => settings.AutoAssignRoles.Any(y => x.Id == y)).ToList();

                var processed = 0;
                var found = 0;
                var result = new StringBuilder();
                await foreach (var batch in guild.GetUsersAsync())
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Autoroles", $"Checking autoroles {processed}/{guild.MemberCount} on server {command.Guild.Name} ({command.GuildId})"));
                    foreach (var user in batch)
                    {
                        if (settings.AutoAssignRoles.All(x => user.RoleIds.Contains(x)))
                            continue;

                        found++;
                        result.Append($"`{user.Username}#{user.Discriminator}` ");
                    }

                    processed += batch.Count;
                    await progressMsg.ModifyAsync(x => x.Content = BuildProgressMessage(processed, guild.MemberCount));
                }

                await progressMsg.DeleteAsync();

                if (found <= 0)
                    await command.ReplySuccess(Communicator, settings.AutoAssignRoles.Count > 1 ? "Everyone has these roles." : "Everyone has this role.");
                else
                    await command.Reply(Communicator, $"**{found}** users are missing at least one role:\n" + result.ToString());
            }
            finally
            {
                TaskHelper.FireForget(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(30));
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Autoroles", $"Removing autorole check command lock for guild {command.GuildId}"));
                    CheckTasks.TryRemove(command.GuildId, out _);
                });
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
