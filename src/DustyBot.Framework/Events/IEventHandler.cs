using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Events
{
    public interface IEventHandler
    {
        Task OnChannelCreated(SocketChannel channel);
        Task OnChannelDestroyed(SocketChannel channel);
        Task OnChannelUpdated(SocketChannel before, SocketChannel after);
        Task OnCurrentUserUpdated(SocketSelfUser before, SocketSelfUser after);
        Task OnGuildAvailable(SocketGuild guild);
        Task OnGuildMembersDownloaded(SocketGuild guild);
        Task OnGuildMemberUpdated(SocketGuildUser before, SocketGuildUser after);
        Task OnGuildUnavailable(SocketGuild guild);
        Task OnGuildUpdated(SocketGuild before, SocketGuild after);
        Task OnJoinedGuild(SocketGuild guild);
        Task OnLeftGuild(SocketGuild guild);
        Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel);
        Task OnMessageReceived(SocketMessage message);
        Task OnMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel);
        Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction);
        Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction);
        Task OnReactionsCleared(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel);
        Task OnRecipientAdded(SocketGroupUser user);
        Task OnRecipientRemoved(SocketGroupUser user);
        Task OnRoleCreated(SocketRole role);
        Task OnRoleDeleted(SocketRole role);
        Task OnRoleUpdated(SocketRole before, SocketRole after);
        Task OnUserBanned(SocketUser user, SocketGuild guild);
        Task OnUserIsTyping(SocketUser user, ISocketMessageChannel channel);
        Task OnUserJoined(SocketGuildUser guildUser);
        Task OnUserLeft(SocketGuildUser guildUser);
        Task OnUserUnbanned(SocketUser user, SocketGuild guild);
        Task OnUserUpdated(SocketUser before, SocketUser after);
        Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after);
    }
}
