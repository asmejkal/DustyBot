using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands
{
    public interface ICommand
    {
        IUserMessage Message { get; }
        ulong GuildId { get; }
        IGuild Guild { get; }

        string Invoker { get; }
        string Body { get; }
        string Prefix { get; }

        Task<IUserMessage> ReplySuccess(Communication.ICommunicator communicator, string message);
        Task<IUserMessage> ReplyError(Communication.ICommunicator communicator, string message);
        Task<ICollection<IUserMessage>> Reply(Communication.ICommunicator communicator, string message);
        Task<ICollection<IUserMessage>> Reply(Communication.ICommunicator communicator, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);

        int ParametersCount { get; }
        ParameterToken GetParameter(int key);
        IEnumerable<ParameterToken> GetParameters();
    }
}
