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
        IUser Author { get; }
        IMessageChannel Channel { get; }

        string Invoker { get; }
        IEnumerable<string> Verbs { get; }
        string Body { get; }
        string Prefix { get; }

        Task<IUserMessage> ReplySuccess(ICommunicator communicator, string message, Embed embed = null);
        Task<IUserMessage> ReplyError(ICommunicator communicator, string message);
        Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message);
        Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);
        Task Reply(ICommunicator communicator, PageCollection pages, bool controlledByInvoker = false, bool resend = false);

        int ParametersCount { get; }
        int GetIndex(string name);
        ParameterToken GetParameter(int key);
        ParameterToken GetParameter(string name);
        IEnumerable<ParameterToken> GetParameters();
        ParameterToken this[int key] { get; }
        ParameterToken this[string name] { get; }
    }
}
