using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Database.Mongo.Collections.DaumCafe.Models;

namespace DustyBot.Service.Services.DaumCafe
{
    public interface IDaumCafeService
    {
        Task<AddCafeFeedResult> AddCafeFeedAsync(Snowflake guildId, Snowflake userId, Uri boardSectionLink, IMessageGuildChannel channel, Guid? credentialId, CancellationToken ct);
        Task<IEnumerable<DaumCafeFeed>> GetCafeFeedsAsync(Snowflake guildId, CancellationToken ct);
        Task<RemoveCafeFeedResult> RemoveCafeFeedAsync(Snowflake guildId, Guid feedId, CancellationToken ct);
        Task ClearCafeFeedsAsync(Snowflake guildId, CancellationToken ct);

        Task<Guid> AddCredentialAsync(Snowflake userId, string login, string password, string description, CancellationToken ct);
        Task<bool> RemoveCredentialAsync(Snowflake userId, Guid credentialId, CancellationToken ct);
        Task ClearCredentialsAsync(Snowflake userId, CancellationToken ct);
        Task<IEnumerable<DaumCafeCredentialInfo>> GetCredentials(Snowflake userId, CancellationToken ct);
    }

    public enum AddCafeFeedResult
    {
        Success,
        SuccessWithoutPreviews,
        TooManyFeeds,
        UnknownCredentials,
        InvalidBoardLink,
        InaccessibleBoard,
        CountryBlock,
        LoginFailed
    }

    public enum RemoveCafeFeedResult
    {
        Success,
        NotFound
    }
}