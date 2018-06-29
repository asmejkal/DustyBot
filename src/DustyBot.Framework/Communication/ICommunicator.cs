using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Communication
{
    public interface ICommunicator
    {
        Task<IUserMessage> CommandReplySuccess(IMessageChannel channel, string message);
        Task<IUserMessage> CommandReplyError(IMessageChannel channel, string message);
        Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message);
        Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);

        //Reactions to framework events
        Task<IUserMessage> CommandReplyMissingPermissions(IMessageChannel channel, Commands.CommandRegistration command, IEnumerable<GuildPermission> missingPermissions);
        Task<IUserMessage> CommandReplyIncorrectParameters(IMessageChannel channel, Commands.CommandRegistration command, string explanation);
        Task<IUserMessage> CommandReplyGenericFailure(IMessageChannel channel, Commands.CommandRegistration command);
    }
}
