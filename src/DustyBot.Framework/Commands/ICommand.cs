using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DustyBot.Framework.Communication;
using Discord.WebSocket;

namespace DustyBot.Framework.Commands
{
    public interface ICommand
    {
        IUserMessage Message { get; }
        ulong GuildId { get; }
        IGuild Guild { get; }

        string Invoker { get; }
        string Verb { get; }
        string Body { get; }
        string Prefix { get; }

        Task<IUserMessage> ReplySuccess(ICommunicator communicator, string message);
        Task<IUserMessage> ReplyError(ICommunicator communicator, string message);
        Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message);
        Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);
        Task Reply(ICommunicator communicator, PageCollection pages, bool controlledByInvoker = false, bool resend = false);

        int ParametersCount { get; }
        ParameterToken GetParameter(int key);
        IEnumerable<ParameterToken> GetParameters();
        ParameterToken this[int key] { get; }
        Remainder Remainder { get; }
    }

    public abstract class Remainder : ParameterToken
    {
        public Remainder(string body, SocketGuild guild) 
            : base(body, guild)
        {
        }

        public abstract ParameterToken After(int count);
    }
}
