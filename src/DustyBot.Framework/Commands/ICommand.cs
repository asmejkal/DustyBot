using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DustyBot.Framework.Communication;

namespace DustyBot.Framework.Commands
{
    public interface ICommand
    {
        ParameterToken this[int key] { get; }
        ParameterToken this[string name] { get; }

        int ParametersCount { get; }
        IUserMessage Message { get; }
        ulong GuildId { get; }
        IGuild Guild { get; }
        IUser Author { get; }
        IMessageChannel Channel { get; }

        string Invoker { get; }
        IEnumerable<string> Verbs { get; }
        string Body { get; }
        string Prefix { get; }

        ParameterToken GetParameter(int key);
        ParameterToken GetParameter(string name);
        IEnumerable<ParameterToken> GetParameters();
        string GetUsage();

        Task<ICollection<IUserMessage>> ReplySuccess(string message, Embed embed = null);
        Task<ICollection<IUserMessage>> ReplyError(string message);
        Task<ICollection<IUserMessage>> Reply(string message);
        Task<ICollection<IUserMessage>> Reply(Embed embed);
        Task<ICollection<IUserMessage>> Reply(string message, Embed embed);
        Task<ICollection<IUserMessage>> Reply(string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);
        Task Reply(PageCollection pages, bool controlledByInvoker = false, bool resend = false);
    }
}
