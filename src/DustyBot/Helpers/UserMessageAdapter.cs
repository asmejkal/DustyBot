using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    internal class UserMessageAdapter : IUserMessage
    {
        private IUserMessage _inner;

        public UserMessageAdapter(IUserMessage inner)
            => _inner = inner;

        public IReadOnlyDictionary<IEmote, ReactionMetadata> Reactions => _inner.Reactions;

        public MessageType Type => _inner.Type;

        public MessageSource Source => _inner.Source;

        public bool IsTTS => _inner.IsTTS;

        public bool IsPinned => _inner.IsPinned;

        private string _content;
        public string Content
        {
            get => _content ?? _inner.Content;
            set => _content = value;
        }

        public DateTimeOffset Timestamp => _inner.Timestamp;

        public DateTimeOffset? EditedTimestamp => _inner.EditedTimestamp;

        public IMessageChannel Channel => _inner.Channel;

        public IUser Author => _inner.Author;

        public IReadOnlyCollection<IAttachment> Attachments => _inner.Attachments;

        public IReadOnlyCollection<IEmbed> Embeds => _inner.Embeds;

        public IReadOnlyCollection<ITag> Tags => _inner.Tags;

        public IReadOnlyCollection<ulong> MentionedChannelIds => _inner.MentionedChannelIds;

        public IReadOnlyCollection<ulong> MentionedRoleIds => _inner.MentionedRoleIds;

        public IReadOnlyCollection<ulong> MentionedUserIds => _inner.MentionedUserIds;

        public DateTimeOffset CreatedAt => _inner.CreatedAt;

        public ulong Id => _inner.Id;

        public MessageActivity Activity => _inner.Activity;

        public MessageApplication Application => _inner.Application;

        public bool IsSuppressed => _inner.IsSuppressed;

        public MessageReference Reference => _inner.Reference;

        public bool MentionedEveryone => _inner.MentionedEveryone;

        public async Task AddReactionAsync(IEmote emote, RequestOptions options = null)
            => await _inner.AddReactionAsync(emote, options);

        public async Task DeleteAsync(RequestOptions options = null)
            => await _inner.DeleteAsync(options);

        public async Task ModifyAsync(Action<MessageProperties> func, RequestOptions options = null)
            => await _inner.ModifyAsync(func, options);

        public async Task PinAsync(RequestOptions options = null)
            => await _inner.PinAsync(options);

        public async Task RemoveAllReactionsAsync(RequestOptions options = null)
            => await _inner.RemoveAllReactionsAsync(options);

        public async Task RemoveReactionAsync(IEmote emote, IUser user, RequestOptions options = null)
            => await _inner.RemoveReactionAsync(emote, user, options);

        public string Resolve(TagHandling userHandling = TagHandling.Name, TagHandling channelHandling = TagHandling.Name, TagHandling roleHandling = TagHandling.Name, TagHandling everyoneHandling = TagHandling.Ignore, TagHandling emojiHandling = TagHandling.Name)
            => _inner.Resolve(userHandling, channelHandling, roleHandling, everyoneHandling, emojiHandling);

        public async Task UnpinAsync(RequestOptions options = null)
            => await _inner.UnpinAsync(options);

        public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetReactionUsersAsync(IEmote emoji, int limit, RequestOptions options = null) 
            => _inner.GetReactionUsersAsync(emoji, limit, options);

        public Task ModifySuppressionAsync(bool suppressEmbeds, RequestOptions options = null)
            => _inner.ModifySuppressionAsync(suppressEmbeds, options);

        public Task RemoveReactionAsync(IEmote emote, ulong userId, RequestOptions options = null)
            => _inner.RemoveReactionAsync(emote, userId, options);

        public Task CrosspostAsync(RequestOptions options = null)
        {
            return _inner.CrosspostAsync(options);
        }

        public Task RemoveAllReactionsForEmoteAsync(IEmote emote, RequestOptions options = null)
        {
            return _inner.RemoveAllReactionsForEmoteAsync(emote, options);
        }
    }
}
