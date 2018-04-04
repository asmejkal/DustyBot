using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Globalization;
using Newtonsoft.Json.Linq;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Settings;
using DustyBot.Settings;

namespace DustyBot.Modules
{
    [Module("Media", "Social media and media outlets.")]
    class MediaModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public IOwnerConfig Config { get; private set; }

        public MediaModule(ICommunicator communicator, ISettingsProvider settings, IOwnerConfig config)
        {
            Communicator = communicator;
            Settings = settings;
            Config = config;
        }

        [Command("views", "Checks how comebacks are doing on YouTube.")]
        public async Task Views(ICommand command)
        {
            var settings = await Settings.Get<IMediaSettings>(command.GuildId, false);
            if (settings == null || settings.YouTubeComebacks.Count <= 0)
            {
                await command.ReplyError(Communicator, "No comeback info has been set. Use the `addComeback` command.").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithTitle("YouTube stats");

            foreach (var comeback in settings.YouTubeComebacks)
            {
                var info = await GetYoutubeInfo(comeback.VideoIds).ConfigureAwait(false);

                TimeSpan timePublished = DateTime.Now.ToUniversalTime() - info.publishedAt;

                embed.AddField(eab => eab.WithName($":tv: {comeback.Name}").WithIsInline(false).WithValue(
                    $"**Views: **{info.views.ToString("N0", new CultureInfo("en-US"))}\n" +
                    $"**Likes: **{info.likes.ToString("N0", new CultureInfo("en-US"))}\n" +
                    $"**Published: **{String.Format("{0}d {1}h {2}min ago", timePublished.Days, timePublished.Hours, timePublished.Minutes)}\n\n"
                    ));
            }

            await command.Message.Channel.SendMessageAsync("", false, embed.Build()).ConfigureAwait(false);
        }

        [Command("addComeback", "Adds media info for a comeback to be used by other commands.")]
        [Parameters(ParameterType.String, ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("[p]addComeback \"ComebackName\" YoutubeVideoId [MoreYoutubeVideoIds]\n\nExample: [p]setcomebackinfo \"Starry Night\" 0FB2EoKTK_Q LjUXm0Zy_dk")]
        public async Task AddComeback(ICommand command)
        {
            var settings = await Settings.Get<IMediaSettings>(command.GuildId).ConfigureAwait(false);
            settings.YouTubeComebacks.Add(new ComebackInfo()
            {
                Name = (string)command.GetParameter(0),
                VideoIds = new HashSet<string>(command.GetParameters().Skip(1).Select(x => (string)x))
            });

            await Settings.Save(settings).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, "Comeback info has been added.").ConfigureAwait(false);
        }

        [Command("removeComeback", "Removes media info for a specified comeback.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("[p]removeComeback ComebackName")]
        public async Task RemoveComeback(ICommand command)
        {
            var settings = await Settings.Get<IMediaSettings>(command.GuildId).ConfigureAwait(false);

            if (settings.YouTubeComebacks.RemoveAll(x => x.Name == command.Body) > 0)
            {
                await Settings.Save(settings).ConfigureAwait(false);
                await command.ReplySuccess(Communicator, $"Comeback info has been removed.").ConfigureAwait(false);
            }
            else
            {
                await command.ReplyError(Communicator, $"Couldn't find comeback info with name `{command.Body}`.").ConfigureAwait(false);
            }
        }

        [Command("addTweetFeed", "Soon™")]
        public Task AddTweetFeed(ICommand command)
        {
            return Task.CompletedTask;
        }

        public struct YoutubeInfo
        {
            public ulong views;
            public ulong likes;
            public DateTime publishedAt;
        }

        public async Task<YoutubeInfo> GetYoutubeInfo(IEnumerable<string> ids)
        {
            string html = string.Empty;
            string url = @"https://www.googleapis.com/youtube/v3/videos?part=statistics,snippet&id=" + String.Join(",", ids) + @"&key=" + Config.YouTubeKey;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            using (HttpWebResponse response = (HttpWebResponse)(await request.GetResponseAsync().ConfigureAwait(false)))
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                html = await reader.ReadToEndAsync().ConfigureAwait(false);
                var json = JObject.Parse(html);
                ulong totalViews = 0;
                ulong totalLikes = 0;
                DateTime firstPublishedAt = DateTime.Now;

                var items = json["items"];
                foreach (var item in items)
                {
                    var statistics = item["statistics"];
                    totalViews += (ulong)statistics["viewCount"];
                    totalLikes += (ulong)statistics["likeCount"];
                    var publishedAt = (DateTime)item["snippet"]["publishedAt"];

                    if (publishedAt < firstPublishedAt)
                        firstPublishedAt = publishedAt;
                }

                YoutubeInfo info = new YoutubeInfo();
                info.views = totalViews;
                info.likes = totalLikes;
                info.publishedAt = firstPublishedAt;
                return info;
            }
        }
    }
}
