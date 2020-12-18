using Discord;
using DustyBot.Framework.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DustyBot.Framework.Communication
{
    public interface ICommunicator
    {
        Task SendMessage(IMessageChannel channel, PageCollection pages, ulong messageOwner = 0, bool resend = false);
        Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, string text);
        Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, Embed embed);
        Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, string text, Embed embed);
        Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, string text, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);

        string FormatSuccess(string message);
        string FormatFailure(string message);

        Task<ICollection<IUserMessage>> CommandReplySuccess(IMessageChannel channel, string message, Embed embed = null);
        Task<ICollection<IUserMessage>> CommandReplyError(IMessageChannel channel, string message);
        Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message);
        Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, Embed embed);
        Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message, Embed embed);
        Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);
        Task CommandReply(IMessageChannel channel, PageCollection pages, ulong messageOwner = 0, bool resend = false);

        Task<ICollection<IUserMessage>> CommandReplyMissingPermissions(IMessageChannel channel, CommandInfo command, IEnumerable<GuildPermission> missingPermissions, string message = null);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotAccess(IMessageChannel channel, CommandInfo command);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, CommandInfo command);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, CommandInfo command, IEnumerable<GuildPermission> missingPermissions);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, CommandInfo command, IEnumerable<ChannelPermission> missingPermissions);
        Task<ICollection<IUserMessage>> CommandReplyNotOwner(IMessageChannel channel, CommandInfo command);
        Task<ICollection<IUserMessage>> CommandReplyIncorrectParameters(IMessageChannel channel, CommandInfo command, string explanation, string commandPrefix, bool showUsage = true);
        Task<ICollection<IUserMessage>> CommandReplyUnclearParameters(IMessageChannel channel, CommandInfo command, string explanation, string commandPrefix, bool showUsage = true);
        Task<ICollection<IUserMessage>> CommandReplyDirectMessageOnly(IMessageChannel channel, CommandInfo command);
        Task<ICollection<IUserMessage>> CommandReplyGenericFailure(IMessageChannel channel);

        string BuildCommandUsage(CommandInfo command, string commandPrefix);
    }
}
