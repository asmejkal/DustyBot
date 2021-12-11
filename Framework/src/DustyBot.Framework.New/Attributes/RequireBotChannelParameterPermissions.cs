﻿using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using DustyBot.Framework.Entities;
using Qmmands;

namespace DustyBot.Framework.Attributes
{
    public class RequireBotChannelParameterPermissions : DiscordGuildParameterCheckAttribute
    {
        public Permission Permissions { get; }

        public RequireBotChannelParameterPermissions(Permission permissions)
        {
            Permissions = permissions;
        }

        public override bool CheckType(Type type)
            => typeof(IGuildChannel).IsAssignableFrom(type);

        public override ValueTask<CheckResult> CheckAsync(object argument, DiscordGuildCommandContext context)
        {
            var channel = (IGuildChannel)argument;
            var permissions = context.Guild.GetBotPermissions(channel);

            return permissions.Has(Permissions) ?
                Success() :
                Failure($"The bot is missing permissions in {Mention.Channel(channel)} ({Permissions & (~permissions)}).");
        }
    }
}
