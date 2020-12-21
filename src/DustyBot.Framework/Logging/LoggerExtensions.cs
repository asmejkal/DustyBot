using Discord;
using DustyBot.Core.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace DustyBot.Framework.Logging
{
    public static class LoggerExtensions
    {
        internal static ILogger WithCommandScope(this ILogger logger, IMessage message, Guid commandId)
        {
            var correlationIds = new Dictionary<string, object>();
            correlationIds.Add(LogFields.CommandId, commandId);
            FillMessage(correlationIds, message);
            FillUser(correlationIds, message.Author);
            FillChannel(correlationIds, message.Channel);

            if (message.Channel is IGuildChannel guildChannel)
                FillGuild(correlationIds, guildChannel.Guild);

            return logger.WithScope(correlationIds);
        }

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
