using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Utility
{
    public static class DiscordExtensions
    {
        public static void DeleteAfter(this IMessage msg, int seconds)
        {
            Task.Run(async () =>
            {
                await Task.Delay(seconds * 1000);
                try { await msg.DeleteAsync().ConfigureAwait(false); }
                catch { }
            });
        }

        public static async Task<ICollection<IUserMessage>> SendLongStringAsync(this IMessageChannel channel, string text, bool isTTS = false, RequestOptions options = null)
        {
            var result = new List<IUserMessage>();
            foreach (var chunk in text.Chunkify(DiscordConfig.MaxMessageSize))
                result.Add(await channel.SendMessageAsync(chunk, isTTS, null, options));

            return result;
        }

        public static async Task<ICollection<IUserMessage>> SendLongStringAsync(this IMessageChannel channel, string text, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0, bool isTTS = false, RequestOptions options = null)
        {
            if (maxDecoratorOverhead >= DiscordConfig.MaxMessageSize)
                throw new ArgumentException($"MaxDecoratorOverhead may not exceed the message length limit, {DiscordConfig.MaxMessageSize}.");

            var result = new List<IUserMessage>();
            foreach (var chunk in text.Chunkify(DiscordConfig.MaxMessageSize - maxDecoratorOverhead))
                result.Add(await channel.SendMessageAsync(chunkDecorator(chunk)));

            return result;
        }
    }
}
