using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using DustyBot.Core.Collections;
using DustyBot.Framework.Services;

namespace DustyBot.Service.Services
{
    internal class ChannelActivityWatcher : DustyBotService, IChannelActivityWatcher
    {
        private readonly Dictionary<(Snowflake userId, Snowflake channelId), HashSet<Guid>> _trackers = new();

        public async Task<bool> WaitForUserActivityAsync(Snowflake userId, Snowflake channelId, TimeSpan timeout, CancellationToken ct)
        {
            var handle = WatchForUserActivity(userId, channelId);
            await Task.Delay(timeout, ct);
            return WasUserActive(handle, userId, channelId);
        }

        public Guid WatchForUserActivity(Snowflake userId, Snowflake channelId)
        {
            lock (_trackers)
            {
                var handle = Guid.NewGuid();

                var handles = _trackers.GetOrAdd((userId, channelId));
                handles.Add(handle);
                return handle;
            }
        }

        public bool WasUserActive(Guid handle, Snowflake userId, Snowflake channelId)
        {
            lock (_trackers)
            {
                if (!_trackers.TryGetValue((userId, channelId), out var handles))
                    return true;

                if (!handles.Remove(handle))
                    return true;

                if (!handles.Any())
                    _trackers.Remove((userId, channelId));

                return false;
            }
        }

        protected override ValueTask OnMessageReceived(BotMessageReceivedEventArgs e)
        {
            ProcessUserActivity(e.AuthorId, e.ChannelId);
            return default;
        }

        protected override ValueTask OnMessageUpdated(MessageUpdatedEventArgs e)
        {
            if (e.NewMessage != null)
                ProcessUserActivity(e.NewMessage.Author.Id, e.ChannelId);

            return default;
        }

        protected override ValueTask OnReactionAdded(ReactionAddedEventArgs e)
        {
            ProcessUserActivity(e.UserId, e.ChannelId);
            return default;
        }

        protected override ValueTask OnReactionRemoved(ReactionRemovedEventArgs e)
        {
            ProcessUserActivity(e.UserId, e.ChannelId);
            return default;
        }

        protected override ValueTask OnTypingStarted(TypingStartedEventArgs e)
        {
            ProcessUserActivity(e.UserId, e.ChannelId);
            return default;
        }

        private void ProcessUserActivity(ulong user, ulong channel)
        {
            lock (_trackers)
            {
                _trackers.Remove((user, channel));
            }
        }
    }
}
