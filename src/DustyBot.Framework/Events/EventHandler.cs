using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Events
{
    public abstract class EventHandler : IEventHandler
    {
        public virtual Task OnChannelCreated(SocketChannel channel) => Task.CompletedTask;
        public virtual Task OnChannelDestroyed(SocketChannel channel) => Task.CompletedTask;
        public virtual Task OnChannelUpdated(SocketChannel before, SocketChannel after) => Task.CompletedTask;
        public virtual Task OnCurrentUserUpdated(SocketSelfUser before, SocketSelfUser after) => Task.CompletedTask;
        public virtual Task OnGuildAvailable(SocketGuild guild) => Task.CompletedTask;
        public virtual Task OnGuildMembersDownloaded(SocketGuild guild) => Task.CompletedTask;
        public virtual Task OnGuildMemberUpdated(SocketGuildUser before, SocketGuildUser after) => Task.CompletedTask;
        public virtual Task OnGuildUnavailable(SocketGuild guild) => Task.CompletedTask;
        public virtual Task OnGuildUpdated(SocketGuild before, SocketGuild after) => Task.CompletedTask;
        public virtual Task OnJoinedGuild(SocketGuild guild) => Task.CompletedTask;
        public virtual Task OnLeftGuild(SocketGuild guild) => Task.CompletedTask;
        public virtual Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel) => Task.CompletedTask;
        public virtual Task OnMessageReceived(SocketMessage message) => Task.CompletedTask;
        public virtual Task OnMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel) => Task.CompletedTask;
        public virtual Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction) => Task.CompletedTask;
        public virtual Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction) => Task.CompletedTask;
        public virtual Task OnReactionsCleared(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel) => Task.CompletedTask;
        public virtual Task OnRecipientAdded(SocketGroupUser user) => Task.CompletedTask;
        public virtual Task OnRecipientRemoved(SocketGroupUser user) => Task.CompletedTask;
        public virtual Task OnRoleCreated(SocketRole role) => Task.CompletedTask;
        public virtual Task OnRoleDeleted(SocketRole role) => Task.CompletedTask;
        public virtual Task OnRoleUpdated(SocketRole before, SocketRole after) => Task.CompletedTask;
        public virtual Task OnUserBanned(SocketUser user, SocketGuild guild) => Task.CompletedTask;
        public virtual Task OnUserIsTyping(SocketUser user, ISocketMessageChannel channel) => Task.CompletedTask;
        public virtual Task OnUserJoined(SocketGuildUser guildUser) => Task.CompletedTask;
        public virtual Task OnUserLeft(SocketGuildUser guildUser) => Task.CompletedTask;
        public virtual Task OnUserUnbanned(SocketUser user, SocketGuild guild) => Task.CompletedTask;
        public virtual Task OnUserUpdated(SocketUser before, SocketUser after) => Task.CompletedTask;
        public virtual Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after) => Task.CompletedTask;
    }
}
