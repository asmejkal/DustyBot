using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord;

namespace DustyBot.Service.Services
{
    internal interface IChannelActivityWatcher
    {
        Task<bool> WaitForUserActivityAsync(Snowflake userId, Snowflake channelId, TimeSpan timeout, CancellationToken ct);
        Guid WatchForUserActivity(Snowflake userId, Snowflake channelId);
        bool WasUserActive(Guid handle, Snowflake userId, Snowflake channelId);
    }
}