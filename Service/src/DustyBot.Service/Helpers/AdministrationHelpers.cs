using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Services;

namespace DustyBot.Service.Helpers
{
    internal static class AdministrationHelpers
    {
        public const string MuteRoleName = "Muted";

        /// <summary>
        /// Mutes a member.
        /// </summary>
        /// <returns>A list of channels that couldn't be muted because of missing permissions.</returns>
        public static async Task<IEnumerable<IGuildChannel>> Mute(IGuildUser user, string reason, ISettingsService settings)
        {
            IRole muteRole = user.Guild.Roles.FirstOrDefault(x => x.Name == MuteRoleName);
            if (muteRole == null)
            {
                muteRole = await user.Guild.CreateRoleAsync(MuteRoleName, permissions: GuildPermissions.None, isMentionable: false);
                if (muteRole == null)
                    throw new InvalidOperationException("Cannot create mute role.");
            }

            var permissionFails = new List<IGuildChannel>();
            // if (bot has permissions to add overrides)
            foreach (var channel in await user.Guild.GetChannelsAsync())
            {
                if (channel is ITextChannel || channel is IVoiceChannel)
                {
                    try
                    {
                        var overwrite = channel.GetPermissionOverwrite(muteRole);
                        if (overwrite == null || overwrite.Value.SendMessages != PermValue.Deny || overwrite.Value.Connect != PermValue.Deny || overwrite.Value.AddReactions != PermValue.Deny)
                            await channel.AddPermissionOverwriteAsync(muteRole, new OverwritePermissions(sendMessages: PermValue.Deny, connect: PermValue.Deny, addReactions: PermValue.Deny));
                    }
                    catch (Discord.Net.HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        permissionFails.Add(channel);
                    }
                }
            }

            var rolesSettings = await settings.Read<RolesSettings>(user.GuildId, false);
            if (rolesSettings == null || !rolesSettings.AdditionalPersistentRoles.Contains(muteRole.Id))
                await settings.Modify(user.GuildId, (RolesSettings x) => x.AdditionalPersistentRoles.Add(muteRole.Id));

            await user.AddRoleAsync(muteRole, new RequestOptions() { AuditLogReason = reason });
            return permissionFails;
        }

        public static async Task Unmute(IGuildUser user)
        {
            IRole muteRole = user.Guild.Roles.FirstOrDefault(x => x.Name == MuteRoleName);
            if (muteRole == null)
                return;

            await user.RemoveRoleAsync(muteRole);
        }
    }
}
