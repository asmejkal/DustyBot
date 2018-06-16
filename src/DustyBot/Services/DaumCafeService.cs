using Discord.WebSocket;
using DustyBot.Framework.Settings;
using DustyBot.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DustyBot.Settings.LiteDB;

namespace DustyBot.Services
{
    class DaumCafeService : IDisposable, Framework.Services.IService
    {
        System.Threading.Timer _timer;

        public ISettingsProvider Settings { get; private set; }
        public IOwnerConfig Config { get; private set; }
        public DiscordSocketClient Client { get; private set; }

        public static readonly TimeSpan UpdateFrequency = TimeSpan.FromMinutes(2);
        bool _updating = false;

        public DaumCafeService(DiscordSocketClient client, ISettingsProvider settings, IOwnerConfig config)
        {
            Settings = settings;
            Config = config;
            Client = client;
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
                            await UpdateFeed(feed, settings.ServerId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    //Log
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

            var lastPostId = await Helpers.DaumCafeHelpers.GetLastPostId(feed.CafeId, feed.BoardId);
            var currentPostId = feed.LastPostId;
            if (lastPostId <= feed.LastPostId)
                return; //TODO: how do Daum IDs behave with deleted posts?

            while (lastPostId > currentPostId)
            {
                await channel.SendMessageAsync($"http://m.cafe.daum.net/{feed.CafeId}/{feed.BoardId}/{currentPostId + 1}");
                currentPostId++;
            }

            await Settings.Modify<MediaSettings>(serverId, settings =>
            {
                var current = settings.DaumCafeFeeds.FirstOrDefault(x => x.Id == feed.Id);
                if (current != null && current.LastPostId < currentPostId)
                    current.LastPostId = currentPostId;
            }).ConfigureAwait(false);
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
                }
                
                _disposed = true;
            }
        }
        
        //~()
        //{
        //    Dispose(false);
        //}
    }
#endregion

}
