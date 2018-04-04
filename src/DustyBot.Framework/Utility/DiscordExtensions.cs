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
    }
}
