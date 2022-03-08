using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Core.Formatting;
using DustyBot.Core.Services;
using DustyBot.Database.Mongo.Collections.DaumCafe;
using DustyBot.Database.Mongo.Collections.DaumCafe.Models;
using DustyBot.Database.Services;
using DustyBot.DaumCafe;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Services;
using DustyBot.Service.Helpers.DaumCafe.Exceptions;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Services.DaumCafe
{
    internal class DaumCafeService : RecurringDustyBotService
    {
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(1);
        private static readonly TimeSpan UpdateFrequency = TimeSpan.FromMinutes(15);
        private readonly IDaumCafePostSender _postSender;
        private readonly DiscordClientBase _client;
        private readonly ISettingsService _settings;
        private readonly ICredentialsService _credentialsService;
        private readonly ILogger<RecurringDustyBotService> _logger;
        private readonly Dictionary<Guid, Tuple<DateTime, DaumCafeSession>> _sessionCache = new();

        public DaumCafeService(
            IDaumCafePostSender postSender,
            DiscordClientBase client,
            ISettingsService settings,
            ICredentialsService credentialsService,
            ITimerAwaiter timerAwaiter,
            IServiceProvider services,
            ILogger<RecurringDustyBotService> logger) 
            : base(UpdateFrequency, timerAwaiter, services, logger, UpdateFrequency)
        {
            _postSender = postSender;
            _client = client;
            _settings = settings;
            _credentialsService = credentialsService;
            _logger = logger;
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
            var session = await GetSessionAsync(feed, ct);

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

        private async Task<DaumCafeSession> GetSessionAsync(DaumCafeFeed feed, CancellationToken ct)
        {
            if (feed.CredentialId == Guid.Empty)
                return DaumCafeSession.Anonymous;

            if (_sessionCache.TryGetValue(feed.CredentialId, out var dateSession) && DateTime.Now - dateSession.Item1 <= SessionLifetime)
                return dateSession.Item2;

            var credentials = await _credentialsService.GetAsync(feed.CredentialUser, feed.CredentialId, ct);
            var session = DaumCafeSession.Anonymous;
            if (credentials != null)
            {
                try
                {
                    session = await DaumCafeSession.Create(credentials.Login, credentials.Password, ct);
                }
                catch (Exception ex) when (ex is CountryBlockException || ex is LoginFailedException)
                {
                    _logger.WithUser(feed.CredentialUser)
                        .LogInformation("Credential {CredentialId} is invalid, proceeding with an anonymous session", feed.CredentialId);
                }
            }
            else
            {
                _logger.WithUser(feed.CredentialUser)
                    .LogInformation("Credential {CredentialId} not found, proceeding with an anonymous session", feed.CredentialId);
            }

            _sessionCache[feed.CredentialId] = Tuple.Create(DateTime.Now, session);
            return session;
        }
    }
}
