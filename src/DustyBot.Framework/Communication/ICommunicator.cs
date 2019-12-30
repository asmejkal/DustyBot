using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Communication
{
    public class Page
    {
        public string Content { get; set; } = string.Empty;
        public EmbedBuilder Embed { get; set; }
    }

    public class PageCollection : List<Page>
    {
        public void Add(string content) => Add(new Page() { Content = content });
        public void Add(EmbedBuilder embed) => Add(new Page() { Embed = embed });

        public Page Last => Count < 1 ? null : this[Count - 1];
        public bool IsEmpty => Count <= 0;
    }

    public interface ICommunicator
    {
        Task<ICollection<IUserMessage>> CommandReplySuccess(IMessageChannel channel, string message, Embed embed = null);
        Task<ICollection<IUserMessage>> CommandReplyError(IMessageChannel channel, string message);
        Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message);
        Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);
        Task CommandReply(IMessageChannel channel, PageCollection pages, ulong messageOwner = 0, bool resend = false);

        Task SendMessage(IMessageChannel channel, PageCollection pages, ulong messageOwner = 0, bool resend = false);
        Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, string text, Embed embed = null);
        Task<ICollection<IUserMessage>> SendMessage(IMessageChannel channel, string text, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0);

        //Reactions to framework events
        Task<ICollection<IUserMessage>> CommandReplyMissingPermissions(IMessageChannel channel, Commands.CommandRegistration command, IEnumerable<GuildPermission> missingPermissions, string message = null);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotAccess(IMessageChannel channel, Commands.CommandRegistration command);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, Commands.CommandRegistration command);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, Commands.CommandRegistration command, IEnumerable<GuildPermission> missingPermissions);
        Task<ICollection<IUserMessage>> CommandReplyMissingBotPermissions(IMessageChannel channel, Commands.CommandRegistration command, IEnumerable<ChannelPermission> missingPermissions);
        Task<ICollection<IUserMessage>> CommandReplyNotOwner(IMessageChannel channel, Commands.CommandRegistration command);
        Task<ICollection<IUserMessage>> CommandReplyIncorrectParameters(IMessageChannel channel, Commands.CommandRegistration command, string explanation, bool showUsage = true);
        Task<ICollection<IUserMessage>> CommandReplyUnclearParameters(IMessageChannel channel, Commands.CommandRegistration command, string explanation, bool showUsage = true);
        Task<ICollection<IUserMessage>> CommandReplyDirectMessageOnly(IMessageChannel channel, Commands.CommandRegistration command);
        Task<ICollection<IUserMessage>> CommandReplyGenericFailure(IMessageChannel channel, Commands.CommandRegistration command);
    }
}
