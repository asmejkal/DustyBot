using Discord.WebSocket;
using DustyBot.Framework.Settings;
using DustyBot.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DustyBot.Settings.LiteDB;
using DustyBot.Helpers;
using DustyBot.Framework.Logging;
using Discord;

namespace DustyBot.Services
{
    class DaumCafeService : IDisposable, Framework.Services.IService
    {
        System.Threading.Timer _timer;

        public ISettingsProvider Settings { get; private set; }
        public DiscordSocketClient Client { get; private set; }
        public ILogger Logger { get; private set; }

        public static readonly TimeSpan UpdateFrequency = TimeSpan.FromMinutes(2);
        bool _updating = false;

        public DaumCafeService(DiscordSocketClient client, ISettingsProvider settings, ILogger logger)
        {
            Settings = settings;
            Client = client;
            Logger = logger;
        }

        public void Start()
        {
            _timer = new System.Threading.Timer(OnUpdate, null, (int)UpdateFrequency.TotalMilliseconds, (int)UpdateFrequency.TotalMilliseconds);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }
        
        async void OnUpdate(object state)
        {
            await Task.Run(async () =>
            {
                if (_updating)
                    return; //Skip if the previous update is still running

                _updating = true;

                try
                {
                    foreach (var settings in await Settings.Read<MediaSettings>().ConfigureAwait(false))
                    {
                        if (settings.DaumCafeFeeds == null || settings.DaumCafeFeeds.Count <= 0)
                            continue;

                        foreach (var feed in settings.DaumCafeFeeds)
                        {
                            try
                            {
                                await UpdateFeed(feed, settings.ServerId);
                            }
                            catch (Exception ex)
                            {
                                await Logger.Log(new LogMessage(LogSeverity.Error, "Service", $"Failed to update Daum Cafe feed {feed.Id} ({feed.CafeId}/{feed.BoardId}) on server {settings.ServerId}.", ex));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Service", "Failed to update Daum Cafe feeds.", ex));
                }
                finally
                {
                    _updating = false;
                }
            });            
        }

        async Task UpdateFeed(DaumCafeFeed feed, ulong serverId)
        {
            var guild = Client.GetGuild(serverId);
            if (guild == null)
                return; //TODO: zombie settings should be cleared

            var channel = guild.GetTextChannel(feed.TargetChannel);
            if (channel == null)
                return; //TODO: zombie settings should be cleared

            var lastPostId = await DaumCafeSession.Anonymous.GetLastPostId(feed.CafeId, feed.BoardId);
            var currentPostId = feed.LastPostId;
            if (lastPostId <= feed.LastPostId)
                return; //TODO: how do Daum IDs behave with deleted posts? does it reuse the ID if the newest post is deleted?

            while (lastPostId > currentPostId)
            {
                var preview = await CreatePreview($"http://m.cafe.daum.net/{feed.CafeId}/{feed.BoardId}/{currentPostId + 1}", feed.CafeId);
                
                await channel.SendMessageAsync(preview.Item1, false, preview.Item2);
                currentPostId++;
            }

            await Settings.Modify<MediaSettings>(serverId, settings =>
            {
                var current = settings.DaumCafeFeeds.FirstOrDefault(x => x.Id == feed.Id);
                if (current != null && current.LastPostId < currentPostId)
                    current.LastPostId = currentPostId;
            }).ConfigureAwait(false);
        }

        private Embed BuildPreview(string title, string url, string description, string imageUrl, string cafeName)
        {
            var embedBuilder = new EmbedBuilder()
                        .WithTitle(title)
                        .WithUrl(url)
                        .WithFooter("Daum Cafe • " + cafeName);

            if (!string.IsNullOrWhiteSpace(description))
                embedBuilder.Description = description.Truncate(350);

            if (!string.IsNullOrWhiteSpace(imageUrl) && !imageUrl.Contains("cafe_meta_image.png"))
                embedBuilder.ImageUrl = imageUrl;

            return embedBuilder.Build();
        }

        public async Task<Tuple<string, Embed>> CreatePreview(string mobileUrl, string cafeName)
        {
            var text = $"<{mobileUrl}>";
            Embed embed = null;
            try
            {
                var metadata = await DaumCafeSession.Anonymous.GetPageMetadata(mobileUrl);
                if (metadata.Type == "article" && !string.IsNullOrWhiteSpace(metadata.Title))
                    embed = BuildPreview(metadata.Title, mobileUrl, metadata.Description, metadata.ImageUrl, cafeName);
                else if (!string.IsNullOrEmpty(metadata.Body.Subject))
                    embed = BuildPreview(metadata.Body.Subject, mobileUrl, metadata.Body.Text, metadata.Body.ImageUrl, cafeName);
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Warning, "Service", "Failed to create Daum Cafe post preview.", ex));
            }

            return Tuple.Create(text, embed);
        }
        
        #region IDisposable 

        private bool _disposed = false;
                
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
                
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _timer?.Dispose();
                    _timer = null;
                }
                
                _disposed = true;
            }
        }

        //~()
        //{
        //    Dispose(false);
        //}

        #endregion
    }

}
