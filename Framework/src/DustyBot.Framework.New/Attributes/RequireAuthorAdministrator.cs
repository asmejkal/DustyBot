﻿using System.Threading.Tasks;
using Disqord.Bot;
using Disqord.Gateway;
using Qmmands;

namespace DustyBot.Framework.Attributes
{
    /// <summary>
    /// Specifies that the module or command can only be executed by authors with the Administrator permission.
    /// </summary>
    public class RequireAuthorAdministrator : DiscordGuildCheckAttribute
    {
        public override ValueTask<CheckResult> CheckAsync(DiscordGuildCommandContext context)
        {
            var permissions = context.Author.GetPermissions();
            if (permissions.Administrator)
                return Success();

            return Failure("Only server administrators can use this command.");
        }
    }
}
