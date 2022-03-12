using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using DustyBot.Core.Security;
using DustyBot.Core.Services;
using DustyBot.Database.Mongo.Collections.DaumCafe;
using DustyBot.Database.Mongo.Collections.DaumCafe.Models;
using DustyBot.Database.Mongo.Models;
using DustyBot.Database.Services;
using DustyBot.DaumCafe;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Services;
using DustyBot.Service.Helpers.DaumCafe.Exceptions;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Services.DaumCafe
{
    internal class DaumCafeService : RecurringDustyBotService, IDaumCafeService
    {
        private const int ServerFeedLimit = 25;
        private static readonly TimeSpan UpdateFrequency = TimeSpan.FromMinutes(15);

        private readonly IDaumCafePostSender _postSender;
        private readonly IDaumCafeSessionManager _sessionManager;
        private readonly IDaumCafeSettingsService _userSettings;
        private readonly DiscordClientBase _client;
        private readonly ISettingsService _settings;
        private readonly ILogger<RecurringDustyBotService> _logger;

        public DaumCafeService(
            IDaumCafePostSender postSender,
            IDaumCafeSessionManager sessionManager,
            IDaumCafeSettingsService userSettings,
            DiscordClientBase client,
            ISettingsService settings,
            ITimerAwaiter timerAwaiter,
            IServiceProvider services,
            ILogger<RecurringDustyBotService> logger)
            : base(UpdateFrequency, timerAwaiter, services, logger, UpdateFrequency)
        {
            _postSender = postSender;
            _sessionManager = sessionManager;
            _userSettings = userSettings;
            _client = client;
            _settings = settings;
            _logger = logger;
        }

        public async Task<AddCafeFeedResult> AddCafeFeedAsync(Snowflake guildId, Snowflake userId, Uri boardSectionLink, IMessageGuildChannel channel, Guid? credentialId, CancellationToken ct)
        {
            var currentSettings = await _settings.Read<DaumCafeSettings>(guildId, false, ct);
            if (currentSettings != null && currentSettings.Feeds.Count >= ServerFeedLimit)
                return AddCafeFeedResult.TooManyFeeds;

            try
            {
                DaumCafeSession session;
                if (credentialId.HasValue)
                {
                    var credential = await _userSettings.GetCredentialAsync(userId, credentialId.Value, ct);
                    if (credential == null)
                        return AddCafeFeedResult.UnknownCredentials;

                    session = await DaumCafeSession.Create(credential.Login, credential.Password, ct);
                }
                else
                {
                    session = DaumCafeSession.Anonymous;
                }

                var (cafeId, boardId) = await session.GetCafeAndBoardId(boardSectionLink);
                var postsAccesible = await session.ArePostsAccesible(cafeId, boardId, ct);
                var feed = new DaumCafeFeed(cafeId, boardId, channel.Id, userId, credentialId ?? default);

                await _settings.Modify(guildId, (DaumCafeSettings x) =>
                {
                    // Remove duplicate feeds
                    x.Feeds.RemoveAll(x => x.CafeId == feed.CafeId && x.BoardId == feed.BoardId && x.TargetChannel == feed.TargetChannel);
                    x.Feeds.Add(feed);
                }, ct);

                return postsAccesible ? AddCafeFeedResult.Success : AddCafeFeedResult.SuccessWithoutPreviews;
            }
            catch (InvalidBoardLinkException)
            {
                return AddCafeFeedResult.InvalidBoardLink;
            }
            catch (InaccessibleBoardException)
            {
                return AddCafeFeedResult.InaccessibleBoard;
            }
            catch (CountryBlockException)
            {
                return AddCafeFeedResult.CountryBlock;
            }
            catch (LoginFailedException)
            {
                return AddCafeFeedResult.LoginFailed;
            }
        }

        public Task<RemoveCafeFeedResult> RemoveCafeFeedAsync(Snowflake guildId, Guid feedId, CancellationToken ct)
        {
            return _settings.Modify(guildId, (DaumCafeSettings x) =>
            {
                return x.Feeds.RemoveAll(x => x.Id == feedId) > 0
                    ? RemoveCafeFeedResult.Success
                    : RemoveCafeFeedResult.NotFound;
            }, ct);
        }

        public Task ClearCafeFeedsAsync(Snowflake guildId, CancellationToken ct)
        {
            return _settings.Modify(guildId, (DaumCafeSettings x) => x.Feeds.Clear(), ct);
        }

        public async Task<IEnumerable<DaumCafeFeed>> GetCafeFeedsAsync(Snowflake guildId, CancellationToken ct)
        {
            var settings = await _settings.Read<DaumCafeSettings>(guildId, false, ct);
            return settings?.Feeds ?? Enumerable.Empty<DaumCafeFeed>();
        }

        public async Task<Guid> AddCredentialAsync(Snowflake userId, string login, string password, string description, CancellationToken ct)
        {
            var credential = new DaumCafeCredential(description, login, password.ToSecureString());
            await _userSettings.AddCredentialAsync(userId, credential, ct);
            return credential.Id;
        }

        public Task<bool> RemoveCredentialAsync(Snowflake userId, Guid credentialId, CancellationToken ct)
        {
            return _userSettings.RemoveCredentialAsync(userId, credentialId, ct);
        }

        public Task ClearCredentialsAsync(Snowflake userId, CancellationToken ct)
        {
            return _userSettings.ResetAsync(userId, ct);
        }

        public async Task<IEnumerable<DaumCafeCredentialInfo>> GetCredentials(Snowflake userId, CancellationToken ct)
        {
            var settings = await _userSettings.ReadAsync(userId, ct);
            if (settings == null)
                return Enumerable.Empty<DaumCafeCredentialInfo>();

            return settings.Credentials.Select(x => new DaumCafeCredentialInfo(x.Id, x.Name));
        }

        protected override async Task ExecuteRecurringAsync(IServiceProvider provider, int executionCount, CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Updating Daum Cafe feeds");

                foreach (var settings in await _settings.Read<DaumCafeSettings>(ct))
                {
                    if (ct.IsCancellationRequested)
                        break;

                    if (settings.Feeds == null || settings.Feeds.Count <= 0)
                        continue;

                    using var scope = _logger.WithGuild(settings.ServerId).BeginScope();
                    foreach (var feed in settings.Feeds)
                    {
                        try
                        {
                            await UpdateFeed(feed, settings.ServerId, ct);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to update Daum Cafe feed {FeedId} ({CafeId}/{BoardId})", feed.Id, feed.CafeId, feed.BoardId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update Daum Cafe feeds");
            }

            _logger.LogInformation("Finished updating Daum Cafe feeds in {TotalElapsed}", stopwatch.Elapsed);
        }

        private async Task UpdateFeed(DaumCafeFeed feed, ulong serverId, CancellationToken ct)
        {
            var guild = _client.GetGuild(serverId);
            if (guild == null)
                return;

            var channel = guild.GetChannel(feed.TargetChannel) as IMessageGuildChannel;
            if (channel == null)
                return;

            using var scope = _logger.WithGuild(guild).WithChannel(channel).BeginScope();
            var session = await _sessionManager.GetSessionAsync(feed, ct);

            // Get last post ID
            int lastPostId;
            try
            {
                lastPostId = await session.GetLastPostId(feed.CafeId, feed.BoardId, ct);
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse r && r.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogInformation("Cafe feed {CafeId}/{BoardId} update forbidden", feed.CafeId, feed.BoardId);
                return;
            }

            // If new feed -> just store the last post ID and return
            if (feed.LastPostId < 0)
            {
                await _settings.Modify<DaumCafeSettings>(serverId, s =>
                {
                    var current = s.Feeds.FirstOrDefault(x => x.Id == feed.Id);
                    if (current != null && current.LastPostId < 0)
                        current.LastPostId = lastPostId;
                }, ct);

                return;
            }

            if (lastPostId <= feed.LastPostId)
                return;

            var currentPostId = feed.LastPostId;
            _logger.LogInformation("Updating feed {CafeId}/{BoardId} found {PostCount} new posts ({FirstPostId} to {LastPostId})", feed.CafeId, feed.BoardId, lastPostId - currentPostId, currentPostId + 1, lastPostId);

            while (lastPostId > currentPostId)
            {
                var page = await session.GetPage(feed.CafeId, feed.BoardId, currentPostId + 1, ct);
                try
                {
                    await _postSender.SendPostAsync(channel, page, ct);
                    currentPostId++;
                }
                catch (MissingPermissionsException)
                {
                    _logger.LogInformation("Can't update Cafe feed because of missing permissions");
                    currentPostId = lastPostId;
                }

                await _settings.Modify<DaumCafeSettings>(serverId, settings =>
                {
                    var current = settings.Feeds.FirstOrDefault(x => x.Id == feed.Id);
                    if (current != null && current.LastPostId < currentPostId)
                        current.LastPostId = currentPostId;
                }, ct);
            }
        }
    }
}
