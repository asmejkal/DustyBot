using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using DustyBot.Framework.Utility;

namespace DustyBot.Framework.Communication
{
    public class DefaultCommunicator : ICommunicator
    {
        public Config.IEssentialConfig Config { get; set; }

        public DefaultCommunicator(Config.IEssentialConfig config)
        {
            Config = config;
        }

        public static readonly Color ColorSuccess = Color.Green;
        public static readonly Color ColorError = Color.Red;

        public async Task<IUserMessage> CommandReplySuccess(IMessageChannel channel, string message) => await channel.SendMessageAsync(":white_check_mark: " + message);
        public async Task<IUserMessage> CommandReplyError(IMessageChannel channel, string message) => await channel.SendMessageAsync(":no_entry: " + message);

        public async Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message) => await channel.SendLongStringAsync(message);
        public async Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0) => await channel.SendLongStringAsync(message, chunkDecorator, maxDecoratorOverhead);

        public async Task<IUserMessage> CommandReplyMissingPermissions(IMessageChannel channel, Commands.CommandRegistration command, IEnumerable<GuildPermission> missingPermissions) =>
            await CommandReplyError(channel, string.Format(Properties.Resources.Command_MissingPermissions, string.Join(", ", missingPermissions)));

        public async Task<IUserMessage> CommandReplyIncorrectParameters(IMessageChannel channel, Commands.CommandRegistration command, string explanation)
        {
            var embed = new EmbedBuilder()
                    .WithTitle(Properties.Resources.Command_Usage)
                    .WithDescription(command.GetUsage(Config.CommandPrefix));

            return await channel.SendMessageAsync(":no_entry: " + Properties.Resources.Command_IncorrectParameters + " " + explanation, false, embed);
        }

        public async Task<IUserMessage> CommandReplyGenericFailure(IMessageChannel channel, Commands.CommandRegistration command) =>
            await CommandReplyError(channel, Properties.Resources.Command_GenericFailure);

        private static async Task<IUserMessage> ReplyEmbed(IMessageChannel channel, string message, Color? color = null, string title = null, string footer = null)
        {
            var embed = new EmbedBuilder()
                    .WithDescription(message);

            if (color != null)
                embed.WithColor(color.Value);

            if (!string.IsNullOrEmpty(title))
                embed.WithTitle(title);

            if (!string.IsNullOrEmpty(footer))
                embed.WithFooter(footer);

            return await channel.SendMessageAsync("", false, embed);
        }
    }
}
