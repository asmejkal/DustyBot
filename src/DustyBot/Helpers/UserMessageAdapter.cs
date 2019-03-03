using Discord;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    class UserMessageAdapter : IUserMessage
    {
        private IUserMessage Inner { get; }

        public UserMessageAdapter(IUserMessage inner)
            => Inner = inner;

        public IReadOnlyDictionary<IEmote, ReactionMetadata> Reactions => Inner.Reactions;

        public MessageType Type => Inner.Type;

        public MessageSource Source => Inner.Source;

        public bool IsTTS => Inner.IsTTS;

        public bool IsPinned => Inner.IsPinned;

        private string _content;
        public string Content
        {
            get => _content ?? Inner.Content;
            set => _content = value;
        }

        public DateTimeOffset Timestamp => Inner.Timestamp;

        public DateTimeOffset? EditedTimestamp => Inner.EditedTimestamp;

        public IMessageChannel Channel => Inner.Channel;

        public IUser Author => Inner.Author;

        public IReadOnlyCollection<IAttachment> Attachments => Inner.Attachments;

        public IReadOnlyCollection<IEmbed> Embeds => Inner.Embeds;

        public IReadOnlyCollection<ITag> Tags => Inner.Tags;

        public IReadOnlyCollection<ulong> MentionedChannelIds => Inner.MentionedChannelIds;

        public IReadOnlyCollection<ulong> MentionedRoleIds => Inner.MentionedRoleIds;

        public IReadOnlyCollection<ulong> MentionedUserIds => Inner.MentionedUserIds;

        public DateTimeOffset CreatedAt => Inner.CreatedAt;

        public ulong Id => Inner.Id;

        public async Task AddReactionAsync(IEmote emote, RequestOptions options = null)
            => await Inner.AddReactionAsync(emote, options);

        public async Task DeleteAsync(RequestOptions options = null)
            => await Inner.DeleteAsync(options);

        public async Task<IReadOnlyCollection<IUser>> GetReactionUsersAsync(IEmote emoji, int limit = 100, ulong? afterUserId = null, RequestOptions options = null)
            => await Inner.GetReactionUsersAsync(emoji, limit, afterUserId, options);

        public async Task ModifyAsync(Action<MessageProperties> func, RequestOptions options = null)
            => await Inner.ModifyAsync(func, options);

        public async Task PinAsync(RequestOptions options = null)
            => await Inner.PinAsync(options);

        public async Task RemoveAllReactionsAsync(RequestOptions options = null)
            => await Inner.RemoveAllReactionsAsync(options);

        public async Task RemoveReactionAsync(IEmote emote, IUser user, RequestOptions options = null)
            => await Inner.RemoveReactionAsync(emote, user, options);

        public string Resolve(TagHandling userHandling = TagHandling.Name, TagHandling channelHandling = TagHandling.Name, TagHandling roleHandling = TagHandling.Name, TagHandling everyoneHandling = TagHandling.Ignore, TagHandling emojiHandling = TagHandling.Name)
            => Inner.Resolve(userHandling, channelHandling, roleHandling, everyoneHandling, emojiHandling);

        public async Task UnpinAsync(RequestOptions options = null)
            => await Inner.UnpinAsync(options);
    }
}
