using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Core.Async;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;
using DustyBot.Helpers;
using Microsoft.Extensions.Logging;

namespace DustyBot.Modules
{
    [Module("Autoroles", "Give roles to all members automatically.")]
    internal sealed class AutorolesModule : IDisposable
    {
        private readonly BaseSocketClient _client;
        private readonly ISettingsService _settings;
        private readonly ILogger<AutorolesModule> _logger;
        private readonly IFrameworkReflector _frameworkReflector;
        private readonly HelpBuilder _helpBuilder;

        private readonly ConcurrentDictionary<ulong, bool> _applyTasks = new ConcurrentDictionary<ulong, bool>();
        private readonly ConcurrentDictionary<ulong, bool> _checkTasks = new ConcurrentDictionary<ulong, bool>();

        public AutorolesModule(
            BaseSocketClient client, 
            ISettingsService settings, 
            ILogger<AutorolesModule> logger, 
            IFrameworkReflector frameworkReflector,
            HelpBuilder helpBuilder)
        {
            _client = client;
            _settings = settings;
            _logger = logger;
            _frameworkReflector = frameworkReflector;
            _helpBuilder = helpBuilder;

            _client.UserJoined += HandleUserJoined;
        }

        [Command("autorole", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("autoroles", "help"), Alias("autorole"), Alias("autoroles")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(_helpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
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
                await command.ReplyError($"The bot doesn't have permission to assign this role to users. Please make sure the role is below the bot's highest role in the server's role list.");
                return;
            }

            await _settings.Modify(command.GuildId, (RolesSettings s) => s.AutoAssignRoles.Add(command[0].AsRole.Id));
            await command.ReplySuccess($"Will now assign role `{command[0].AsRole.Name} ({command[0].AsRole.Id})` to users upon joining.");
        }

        [Command("autorole", "remove", "Remove an automatically assigned role.")]
        [Alias("autoroles", "remove")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("RoleNameOrID", ParameterType.Role, ParameterFlags.Remainder)]
        public async Task RemoveAutoRole(ICommand command)
        {
            var removed = await _settings.Modify(command.GuildId, (RolesSettings s) => s.AutoAssignRoles.Remove(command[0].AsRole.Id));

            if (removed)
                await command.ReplySuccess($"Will no longer assign role `{command[0].AsRole.Name} ({command[0].AsRole.Id})`.");
            else
                await command.ReplyError($"This role is not being assigned automatically.");
        }

        [Command("autorole", "list", "Lists all automatically assigned roles.")]
        [Alias("autoroles", "list")]
        [Permissions(GuildPermission.ManageRoles)]
        public async Task ListAutoRole(ICommand command)
        {
            var settings = await _settings.Read<RolesSettings>(command.GuildId);
            if (settings.AutoAssignRoles.Count <= 0)
            {
                await command.ReplyError("No automatically assigned roles have been set.");
                return;
            }

            var result = string.Empty;
            foreach (var role in settings.AutoAssignRoles)
            {
                result += $"\nId: `{role}` Name: `{command.Guild.Roles.FirstOrDefault(x => x.Id == role)?.Name ?? "DOES NOT EXIST"}`";
            }

            await command.Reply(result);
        }

        [Command("autorole", "apply", "Assigns the current automatic roles to everyone.")]
        [Alias("autoroles", "apply")]
        [Permissions(GuildPermission.Administrator), BotPermissions(GuildPermission.ManageRoles)]
        [Comment("May take a while to complete.")]
        public async Task ApplyAutoRole(ICommand command, ILogger logger)
        {
            var guild = (SocketGuild)command.Guild;
            if (guild.MemberCount > 20000)
            {
                await command.ReplyError("Your server is too large for this command.");
                return;
            }

            var settings = await _settings.Read<RolesSettings>(command.GuildId);
            if (settings.AutoAssignRoles.Count <= 0)
            {
                await command.ReplyError("No automatically assigned roles have been set.");
                return;
            }

            var added = _applyTasks.TryAdd(command.GuildId, true); // TODO: implement command rate limits properly
            if (!added)
                throw new AbortException("Please wait a while before running this command again.");

            try
            {
                string BuildProgressMessage(int processed, int total) => $"This may take a while... assigning roles to users: `{processed}/{total}`";

                var progressMsg = (await command.Reply(BuildProgressMessage(0, guild.MemberCount))).First();
                var roles = command.Guild.Roles.Where(x => settings.AutoAssignRoles.Any(y => x.Id == y)).ToList();

                var processed = 0;
                var assignments = 0;
                var failed = 0;
                await foreach (var batch in guild.GetUsersAsync())
                {
                    logger.LogInformation("Applying autoroles {Progress}/{ProgressGoal}", processed, guild.MemberCount);
                    foreach (var user in batch)
                    {
                        try
                        {
                            foreach (var role in roles.Where(x => !user.RoleIds.Contains(x.Id)))
                            {
                                assignments++;
                                await user.AddRoleAsync(role);
                            }
                        }
                        catch (Discord.Net.HttpException ex) when (ex.HttpCode == HttpStatusCode.Unauthorized || ex.HttpCode == HttpStatusCode.Forbidden)
                        {
                            throw new CommandException("The bot doesn't have the necessary permissions. Please make sure all of the assigned roles are placed below the bot's highest role.");
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            if (failed > 20)
                                throw new CommandException($"Something went wrong and the task was aborted. Assigned roles to {processed} users.");
                                
                            logger.LogError(ex, "Failed to apply autoroles {AutoroleIds}", roles.Select(x => x.Id));
                        }

                        processed++;
                        if ((assignments > 0 && assignments % 50 == 0) || processed % 1000 == 0)
                            await progressMsg.ModifyAsync(x => x.Content = BuildProgressMessage(processed, guild.MemberCount));
                    }
                }

                await progressMsg.DeleteAsync();
                await command.ReplySuccess($"Roles have been assigned to all users" + (failed > 0 ? $" ({failed} failed)." : "."));
            }
            finally
            {
                TaskHelper.FireForget(async () =>
                {
                    await Task.Delay(TimeSpan.FromHours(1));
                    logger.LogInformation("Removing autorole apply command lock");
                    _applyTasks.TryRemove(command.GuildId, out _);
                });
            }
        }

        [Command("autorole", "check", "Checks for users who are missing an automatic role.")]
        [Alias("autoroles", "check")]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        public async Task CheckAutoRole(ICommand command, ILogger logger)
        {
            var settings = await _settings.Read<RolesSettings>(command.GuildId);
            if (settings.AutoAssignRoles.Count <= 0)
            {
                await command.ReplyError("No automatically assigned roles have been set.");
                return;
            }

            var added = _checkTasks.TryAdd(command.GuildId, true); // TODO: implement command rate limits properly
            if (!added)
                throw new AbortException("Please wait a while before running this command again.");

            try
            {
                string BuildProgressMessage(int processed, int total) => $"This may take a while... checking users: `{processed}/{total}`";

                var guild = (SocketGuild)command.Guild;
                var progressMsg = (await command.Reply(BuildProgressMessage(0, guild.MemberCount))).First();
                var roles = command.Guild.Roles.Where(x => settings.AutoAssignRoles.Any(y => x.Id == y)).ToList();

                var processed = 0;
                var found = 0;
                var shown = 0;
                var result = new StringBuilder();
                await foreach (var batch in guild.GetUsersAsync())
                {
                    logger.LogInformation("Checking autoroles {Progress}/{ProgressGoal}", processed, guild.MemberCount);
                    foreach (var user in batch)
                    {
                        if (settings.AutoAssignRoles.All(x => user.RoleIds.Contains(x)))
                            continue;

                        found++;

                        var username = $"`{user.Username}#{user.Discriminator}` ";
                        if (result.Length + username.Length <= 1800)
                        {
                            result.Append(username);
                            shown++;
                        }
                    }

                    processed += batch.Count;
                    await progressMsg.ModifyAsync(x => x.Content = BuildProgressMessage(processed, guild.MemberCount));
                }

                await progressMsg.DeleteAsync();

                if (found > shown)
                    result.Append($" and **{found - shown}** more...");

                if (found <= 0)
                    await command.ReplySuccess(settings.AutoAssignRoles.Count > 1 ? "Everyone has these roles." : "Everyone has this role.");
                else
                    await command.Reply($"**{found}** users are missing at least one role:\n" + result.ToString());
            }
            finally
            {
                TaskHelper.FireForget(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(30));
                    logger.LogInformation("Removing autorole check command lock");
                    _checkTasks.TryRemove(command.GuildId, out _);
                });
            }
        }

        public void Dispose()
        {
            _client.UserJoined -= HandleUserJoined;
        }

        private Task HandleUserJoined(SocketGuildUser guildUser)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var settings = await _settings.Read<RolesSettings>(guildUser.Guild.Id, false);
                    if (settings == null)
                        return;

                    if (settings.AutoAssignRoles.Count <= 0)
                        return;
                    
                    var roles = guildUser.Guild.Roles.Where(x => settings.AutoAssignRoles.Contains(x.Id)).ToList();
                    if (roles.Count <= 0)
                        return;

                    _logger.WithScope(guildUser).LogInformation("Auto-assigning {Count} role(s)", roles.Count);

                    await guildUser.AddRolesAsync(roles);
                }
                catch (Exception ex)
                {
                    _logger.WithScope(guildUser).LogError(ex, "Failed to process greeting event");
                }
            });

            return Task.CompletedTask;
        }
    }
}
