using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace DustyBot.Framework.Logging
{
    public class ConsoleLogger : ILogger
    {
        public ConsoleLogger(DiscordSocketClient client)
        {
            client.Log += Log;
        }

        public Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }
    }
}
