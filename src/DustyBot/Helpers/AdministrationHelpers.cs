using Discord;
using DustyBot.Framework.Settings;
using DustyBot.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    static class AdministrationHelpers
    {
        public const string MuteRoleName = "Muted";

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
    }
}
