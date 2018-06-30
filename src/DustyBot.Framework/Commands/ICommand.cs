using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DustyBot.Framework.Communication;

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

        Task<IUserMessage> ReplySuccess(ICommunicator communicator, string message);
        Task<IUserMessage> ReplyError(ICommunicator communicator, string message);
        Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message);
        Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);
        Task Reply(ICommunicator communicator, PageCollection pages, bool controlledByInvoker = false);

        int ParametersCount { get; }
        ParameterToken GetParameter(int key);
        IEnumerable<ParameterToken> GetParameters();
    }
}
