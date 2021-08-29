using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Core.Formatting;
using DustyBot.Core.Services;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Utility;
using DustyBot.Service.Helpers.DaumCafe;
using DustyBot.Service.Helpers.DaumCafe.Exceptions;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Services
{
    internal sealed class DaumCafeService : RecurringTaskService
    {
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(1);
        private static readonly TimeSpan UpdateFrequency = TimeSpan.FromMinutes(15);

        private readonly ISettingsService _settings;
        private readonly ICredentialsService _credentialsService;
        private readonly ILogger<DaumCafeService> _logger;
        private readonly BaseSocketClient _client;

        private readonly Dictionary<Guid, Tuple<DateTime, DaumCafeSession>> _sessionCache = new Dictionary<Guid, Tuple<DateTime, DaumCafeSession>>();

        public DaumCafeService(
            BaseSocketClient client, 
            ISettingsService settings,
            ICredentialsService credentialsService,
            ILogger<DaumCafeService> logger, 
            ITimerAwaiter timerAwaiter, 
            IServiceProvider services)
            : base(UpdateFrequency, timerAwaiter, services, logger, UpdateFrequency)
        {
            _settings = settings;
            _credentialsService = credentialsService;
            _logger = logger;
            _client = client;
        }

        protected override async Task ExecuteRecurringAsync(IServiceProvider provider, int executionCount, CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Updating Daum Cafe feeds");

                foreach (var settings in await _settings.Read<MediaSettings>())
                {
                    if (ct.IsCancellationRequested)
                        break;

                    if (settings.DaumCafeFeeds == null || settings.DaumCafeFeeds.Count <= 0)
                        continue;

                    foreach (var feed in settings.DaumCafeFeeds)
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
                            _logger.LogError(ex, "Failed to update Daum Cafe feed {FeedId} ({CafeId}/{BoardId}) on server {" + LogFields.GuildId + "}", feed.Id, feed.CafeId, feed.BoardId, settings.ServerId);
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

            var channel = guild.GetTextChannel(feed.TargetChannel);
            if (channel == null)
                return;

            var logger = _logger.WithScope(channel);

            // Choose a session
            DaumCafeSession session;
            if (feed.CredentialId != Guid.Empty)
            {
                if (!_sessionCache.TryGetValue(feed.CredentialId, out var dateSession) || DateTime.Now - dateSession.Item1 > SessionLifetime)
                {
                    var credential = await Modules.CafeModule.GetCredential(_credentialsService, feed.CredentialUser, feed.CredentialId, ct);
                    try
                    {
                        session = await DaumCafeSession.Create(credential.Login, credential.Password, ct);
                        _sessionCache[feed.CredentialId] = Tuple.Create(DateTime.Now, session);
                    }
                    catch (Exception ex) when (ex is CountryBlockException || ex is LoginFailedException)
                    {
                        session = DaumCafeSession.Anonymous;
                        _sessionCache[feed.CredentialId] = Tuple.Create(DateTime.Now, session);
                    }
                }
                else
                {
                    session = dateSession.Item2;
                }
            }
            else
            {
                session = DaumCafeSession.Anonymous;
            }

            // Get last post ID
            int lastPostId;
            try
            {
                lastPostId = await session.GetLastPostId(feed.CafeId, feed.BoardId, ct);
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse r && r.StatusCode == HttpStatusCode.Forbidden)
            {
                logger.LogInformation("Cafe feed {CafeId}/{BoardId} update forbidden", feed.CafeId, feed.BoardId);
                return;
            }

            // If new feed -> just store the last post ID and return
            if (feed.LastPostId < 0)
            {
                await _settings.Modify<MediaSettings>(serverId, s =>
                {
                    var current = s.DaumCafeFeeds.FirstOrDefault(x => x.Id == feed.Id);
                    if (current != null && current.LastPostId < 0)
                        current.LastPostId = lastPostId;
                });

                return;
            }
            
            var currentPostId = feed.LastPostId;
            if (lastPostId <= feed.LastPostId)
                return;

            logger.LogInformation("Updating feed {CafeId}/{BoardId} found {PostCount} new posts ({FirstPostId} to {LastPostId})", feed.CafeId, feed.BoardId, lastPostId - currentPostId, currentPostId + 1, lastPostId, guild.Name, guild.Id);

            while (lastPostId > currentPostId)
            {
                var preview = await CreatePreview(session, feed.CafeId, feed.BoardId, currentPostId + 1, ct);

                if (!guild.CurrentUser.GetPermissions(channel).SendMessages)
                {
                    logger.LogInformation("Can't update Cafe feed because of permissions");
                    currentPostId = lastPostId;
                    break;
                }

                await channel.SendMessageAsync(preview.Item1.Sanitise(), false, preview.Item2);
                currentPostId++;
            }

            await _settings.Modify<MediaSettings>(serverId, settings =>
            {
                var current = settings.DaumCafeFeeds.FirstOrDefault(x => x.Id == feed.Id);
                if (current != null && current.LastPostId < currentPostId)
                    current.LastPostId = currentPostId;
            });
        }

        private Embed BuildPreview(string title, string url, string description, string imageUrl, string cafeName)
        {
            var embedBuilder = new EmbedBuilder()
                        .WithTitle(title)
                        .WithUrl(url)
                        .WithFooter("Daum Cafe • " + cafeName);

            if (!string.IsNullOrWhiteSpace(description))
                embedBuilder.Description = description.JoinWhiteLines(2).TruncateLines(13, trim: true).Truncate(350);

            if (!string.IsNullOrWhiteSpace(imageUrl) && !imageUrl.Contains("cafe_meta_image.png"))
                embedBuilder.ImageUrl = imageUrl;

            return embedBuilder.Build();
        }

        private async Task<Tuple<string, Embed>> CreatePreview(DaumCafeSession session, string cafeId, string boardId, int postId, CancellationToken ct)
        {
            var mobileUrl = $"http://m.cafe.daum.net/{cafeId}/{boardId}/{postId}";
            var desktopUrl = $"http://cafe.daum.net/{cafeId}/{boardId}/{postId}";

            var text = $"<{desktopUrl}>";
            Embed embed = null;
            try
            {
                var metadata = await session.GetPage(new Uri(mobileUrl), ct);
                if (metadata.Type == "comment" && (!string.IsNullOrWhiteSpace(metadata.Body.Text) || !string.IsNullOrWhiteSpace(metadata.ImageUrl)))
                {
                    embed = BuildPreview("New memo", mobileUrl, metadata.Body.Text, metadata.Body.ImageUrl, cafeId);
                }
                else if (!string.IsNullOrEmpty(metadata.Body.Subject) && (!string.IsNullOrWhiteSpace(metadata.Body.Text) || !string.IsNullOrWhiteSpace(metadata.ImageUrl)))
                {
                    embed = BuildPreview(metadata.Body.Subject, mobileUrl, metadata.Body.Text, metadata.Body.ImageUrl, cafeId);
                }
                else if (metadata.Type == "article" && !string.IsNullOrWhiteSpace(metadata.Title) && (!string.IsNullOrWhiteSpace(metadata.Description) || !string.IsNullOrWhiteSpace(metadata.ImageUrl)))
                {
                    embed = BuildPreview(metadata.Title, mobileUrl, metadata.Description, metadata.ImageUrl, cafeId);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create Daum Cafe post preview for {PostUri}", mobileUrl);
            }

            return Tuple.Create(text, embed);
        }
    }
}
