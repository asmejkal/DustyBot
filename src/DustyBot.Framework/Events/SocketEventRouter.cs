using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using DustyBot.Core.Collections;

namespace DustyBot.Framework.Events
{
    class SocketEventRouter : IEventRouter
    {
        public IEnumerable<IEventHandler> Handlers => _handlers;
        public void Register(IEventHandler handler) => _handlers.Add(handler);

        private HashSet<IEventHandler> _handlers;
        private BaseSocketClient _client;

        public SocketEventRouter(IEnumerable<IEventHandler> handlers, BaseSocketClient client)
        {
            _handlers = new HashSet<IEventHandler>(handlers);
            _client = client;
        }

        public void Start()
        {
            _client.ChannelCreated += Client_ChannelCreated;
            _client.ChannelDestroyed += Client_ChannelDestroyed;
            _client.ChannelUpdated += Client_ChannelUpdated;
            _client.CurrentUserUpdated += Client_CurrentUserUpdated;
            _client.GuildAvailable += Client_GuildAvailable;
            _client.GuildMembersDownloaded += Client_GuildMembersDownloaded;
            _client.GuildMemberUpdated += Client_GuildMemberUpdated;
            _client.GuildUnavailable += Client_GuildUnavailable;
            _client.GuildUpdated += Client_GuildUpdated;
            _client.JoinedGuild += Client_JoinedGuild;
            _client.LeftGuild += Client_LeftGuild;
            _client.MessageDeleted += Client_MessageDeleted;
            _client.MessageReceived += Client_MessageReceived;
            _client.MessageUpdated += Client_MessageUpdated;
            _client.ReactionAdded += Client_ReactionAdded;
            _client.ReactionRemoved += Client_ReactionRemoved;
            _client.ReactionsCleared += Client_ReactionsCleared;
            _client.RecipientAdded += Client_RecipientAdded;
            _client.RecipientRemoved += Client_RecipientRemoved;
            _client.RoleCreated += Client_RoleCreated;
            _client.RoleDeleted += Client_RoleDeleted;
            _client.RoleUpdated += Client_RoleUpdated;
            _client.UserBanned += Client_UserBanned;
            _client.UserIsTyping += Client_UserIsTyping;
            _client.UserJoined += Client_UserJoined;
            _client.UserLeft += Client_UserLeft;
            _client.UserUnbanned += Client_UserUnbanned;
            _client.UserUpdated += Client_UserUpdated;
            _client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
            _client.MessagesBulkDeleted += Client_MessagesBulkDeleted;
        }

        private async Task Client_MessagesBulkDeleted(IReadOnlyCollection<Discord.Cacheable<Discord.IMessage, ulong>> arg1, ISocketMessageChannel arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnMessagesBulkDeleted(arg1, arg2));
        }

        private async Task Client_UserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            await _handlers.ForEachAsync(async x => await x.OnUserVoiceStateUpdated(arg1, arg2, arg3));
        }

        private async Task Client_UserUpdated(SocketUser arg1, SocketUser arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnUserUpdated(arg1, arg2));
        }

        private async Task Client_UserUnbanned(SocketUser arg1, SocketGuild arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnUserUnbanned(arg1,  arg2));
        }

        private async Task Client_UserLeft(SocketGuildUser arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnUserLeft(arg));
        }

        private async Task Client_UserJoined(SocketGuildUser arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnUserJoined(arg));
        }

        private async Task Client_UserIsTyping(SocketUser arg1, ISocketMessageChannel arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnUserIsTyping(arg1, arg2));
        }

        private async Task Client_UserBanned(SocketUser arg1, SocketGuild arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnUserBanned(arg1,  arg2));
        }

        private async Task Client_RoleUpdated(SocketRole arg1, SocketRole arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnRoleUpdated(arg1, arg2));
        }

        private async Task Client_RoleDeleted(SocketRole arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnRoleDeleted(arg));
        }

        private async Task Client_RoleCreated(SocketRole arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnRoleCreated(arg));
        }

        private async Task Client_RecipientRemoved(SocketGroupUser arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnRecipientRemoved(arg));
        }

        private async Task Client_RecipientAdded(SocketGroupUser arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnRecipientAdded(arg));
        }

        private async Task Client_ReactionsCleared(Discord.Cacheable<Discord.IUserMessage, ulong> arg1, ISocketMessageChannel arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnReactionsCleared(arg1, arg2));
        }

        private async Task Client_ReactionRemoved(Discord.Cacheable<Discord.IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            await _handlers.ForEachAsync(async x => await x.OnReactionRemoved(arg1, arg2, arg3));
        }

        private async Task Client_ReactionAdded(Discord.Cacheable<Discord.IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            await _handlers.ForEachAsync(async x => await x.OnReactionAdded(arg1, arg2, arg3));
        }

        private async Task Client_MessageUpdated(Discord.Cacheable<Discord.IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            await _handlers.ForEachAsync(async x => await x.OnMessageUpdated(arg1, arg2, arg3));
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnMessageReceived(arg));
        }

        private async Task Client_MessageDeleted(Discord.Cacheable<Discord.IMessage, ulong> arg1, ISocketMessageChannel arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnMessageDeleted(arg1, arg2));
        }

        private async Task Client_LeftGuild(SocketGuild arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnLeftGuild(arg));
        }

        private async Task Client_JoinedGuild(SocketGuild arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnJoinedGuild(arg));
        }

        private async Task Client_GuildUpdated(SocketGuild arg1, SocketGuild arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnGuildUpdated(arg1, arg2));
        }

        private async Task Client_GuildUnavailable(SocketGuild arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnGuildUnavailable(arg));
        }

        private async Task Client_GuildMemberUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnGuildMemberUpdated(arg1, arg2));
        }

        private async Task Client_GuildMembersDownloaded(SocketGuild arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnGuildMembersDownloaded( arg));
        }

        private async Task Client_GuildAvailable(SocketGuild arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnGuildAvailable( arg));
        }

        private async Task Client_CurrentUserUpdated(SocketSelfUser arg1, SocketSelfUser arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnCurrentUserUpdated(arg1, arg2));
        }

        private async Task Client_ChannelUpdated(SocketChannel arg1, SocketChannel arg2)
        {
            await _handlers.ForEachAsync(async x => await x.OnChannelUpdated(arg1, arg2));
        }

        private async Task Client_ChannelDestroyed(SocketChannel arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnChannelDestroyed(arg));
        }

        private async Task Client_ChannelCreated(SocketChannel arg)
        {
            await _handlers.ForEachAsync(async x => await x.OnChannelCreated(arg));
        }
    }
}
