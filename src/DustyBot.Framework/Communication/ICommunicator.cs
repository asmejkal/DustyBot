using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DustyBot.Framework.Communication
{
    public interface ICommunicator
    {
        string SuccessMarker { get; }
        string FailureMarker { get; }

        Task<ICollection<IUserMessage>> CommandReplySuccess(IMessageChannel channel, string message, Embed embed = null);
        Task<ICollection<IUserMessage>> CommandReplyError(IMessageChannel channel, string message);
        Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message);
        Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);
        Task CommandReply(IMessageChannel channel, PageCollection pages, ulong messageOwner = 0, bool resend = false);

        Task SendMessage(IMessageChannel channel, PageCollection pages, ulong messageOwner = 0, bool resend = false);
        Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, string text, Embed embed = null);
        Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, string text, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);

        //Reactions to framework events
        Task<ICollection<IUserMessage>> CommandReplyMissingPermissions(IMessageChannel channel, Commands.CommandInfo command, IEnumerable<GuildPermission> missingPermissions, string message = null);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotAccess(IMessageChannel channel, Commands.CommandInfo command);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, Commands.CommandInfo command);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, Commands.CommandInfo command, IEnumerable<GuildPermission> missingPermissions);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, Commands.CommandInfo command, IEnumerable<ChannelPermission> missingPermissions);
        Task<ICollection<IUserMessage>> CommandReplyNotOwner(IMessageChannel channel, Commands.CommandInfo command);
        Task<ICollection<IUserMessage>> CommandReplyIncorrectParameters(IMessageChannel channel, Commands.CommandInfo command, string explanation, string commandPrefix, bool showUsage = true);
        Task<ICollection<IUserMessage>> CommandReplyUnclearParameters(IMessageChannel channel, Commands.CommandInfo command, string explanation, string commandPrefix, bool showUsage = true);
        Task<ICollection<IUserMessage>> CommandReplyDirectMessageOnly(IMessageChannel channel, Commands.CommandInfo command);
        Task<ICollection<IUserMessage>> CommandReplyGenericFailure(IMessageChannel channel);
    }
}
