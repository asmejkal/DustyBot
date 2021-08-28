using System;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using DustyBot.Core.Logging;
using DustyBot.Framework.Commands;
using Microsoft.Extensions.Logging;

namespace DustyBot.Framework.Logging
{
    public static class LoggerExtensions
    {
        public static ILogger WithScope(this ILogger logger, IMessage message)
        {
            var correlationIds = new Dictionary<string, object>();
            FillMessage(correlationIds, message);
            FillUser(correlationIds, message.Author);
            FillChannel(correlationIds, message.Channel);

            if (message.Channel is IGuildChannel guildChannel)
                FillGuild(correlationIds, guildChannel.Guild);

            return logger.WithScope(correlationIds);
        }

        public static ILogger WithScope(this ILogger logger, IChannel channel)
        {
            var correlationIds = new Dictionary<string, object>();
            FillChannel(correlationIds, channel);

            if (channel is IGuildChannel guildChannel)
                FillGuild(correlationIds, guildChannel.Guild);

            return logger.WithScope(correlationIds);
        }

        public static ILogger WithScope(this ILogger logger, IChannel channel, ulong messageId)
        {
            var correlationIds = new Dictionary<string, object>();
            FillMessage(correlationIds, messageId);
            FillChannel(correlationIds, channel);

            if (channel is IGuildChannel guildChannel)
                FillGuild(correlationIds, guildChannel.Guild);

            return logger.WithScope(correlationIds);
        }

        public static ILogger WithScope(this ILogger logger, IUser user)
        {
            var correlationIds = new Dictionary<string, object>();
            FillUser(correlationIds, user);

            if (user is IGuildUser guildUser)
                FillGuild(correlationIds, guildUser.Guild);

            return logger.WithScope(correlationIds);
        }

        public static ILogger WithScope(this ILogger logger, IGuild guild)
        {
            var correlationIds = new Dictionary<string, object>();
            FillGuild(correlationIds, guild);

            return logger.WithScope(correlationIds);
        }

        public static ILogger WithScope(this ILogger logger, SocketReaction reaction)
        {
            var correlationIds = new Dictionary<string, object>();
            if (reaction.User.IsSpecified)
                FillUser(correlationIds, reaction.User.Value);
            else
                correlationIds.Add(LogFields.UserId, reaction.UserId);

            FillMessage(correlationIds, reaction.MessageId);
            FillChannel(correlationIds, reaction.Channel);
            if (reaction.Channel is IGuildChannel guildChannel)
                FillGuild(correlationIds, guildChannel.Guild);

            return logger.WithScope(correlationIds);
        }

        internal static ILogger WithCommandScope(this ILogger logger, IMessage message, Guid commandId, CommandInfo commandInfo, CommandInfo.Usage commandUsage)
        {
            var correlationIds = new Dictionary<string, object>();
            correlationIds.Add(LogFields.CommandId, commandId);
            correlationIds.Add(LogFields.CommandPrimaryUsage, commandInfo.PrimaryUsage.InvokeUsage);
            correlationIds.Add(LogFields.CommandInvokeUsage, commandUsage.InvokeUsage);
            correlationIds.Add(LogFields.CommandModuleType, commandInfo.ModuleType.Name);

            FillMessage(correlationIds, message);
            FillUser(correlationIds, message.Author);
            FillChannel(correlationIds, message.Channel);

            if (message.Channel is IGuildChannel guildChannel)
                FillGuild(correlationIds, guildChannel.Guild);

            return logger.WithScope(correlationIds);
        }

        private static void FillUser(Dictionary<string, object> correlationIds, IUser user)
        {
            correlationIds.Add(LogFields.UserId, user.Id);
            correlationIds.Add(LogFields.UserName, user.Username);
        }

        private static void FillMessage(Dictionary<string, object> correlationIds, IMessage message)
        {
            FillMessage(correlationIds, message.Id);
        }

        private static void FillMessage(Dictionary<string, object> correlationIds, ulong id)
        {
            correlationIds.Add(LogFields.MessageId, id);
        }

        private static void FillChannel(Dictionary<string, object> correlationIds, IChannel channel)
        {
            correlationIds.Add(LogFields.ChannelId, channel.Id);
            correlationIds.Add(LogFields.ChannelName, channel.Name);
        }

        private static void FillGuild(Dictionary<string, object> correlationIds, IGuild guild)
        {
            correlationIds.Add(LogFields.GuildId, guild.Id);
            correlationIds.Add(LogFields.GuildName, guild.Name);
        }
    }
}
