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
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using System.Threading;

namespace DustyBot.Modules
{
    [Module("Views", "Tracks comeback stats on YouTube.")]
    class ViewsModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }

        public ViewsModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }
        
        [Command("views", "Checks how comebacks are doing on YouTube."), RunAsync]
        [Usage("{p}views [CategoryName]")]
        public async Task Views(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId);
            string category = string.IsNullOrWhiteSpace(command.Body) ? null : command.Body;
            var comebacks = settings.YouTubeComebacks.Where(x => string.Compare(x.Category, category, StringComparison.CurrentCultureIgnoreCase) == 0)
                .Reverse()
                .ToList();

            if (comebacks.Count <= 0)
            {
                await command.ReplyError(Communicator, "No comeback info has been set for this category. Use the `views add` command.").ConfigureAwait(false);
                return;
            }

            var config = await Settings.ReadGlobal<BotConfig>();
            var pages = new PageCollection();
            var infos = new List<Tuple<ComebackInfo, YoutubeInfo>>();
            foreach (var comeback in comebacks)
                infos.Add(Tuple.Create(comeback, await GetYoutubeInfo(comeback.VideoIds, config.YouTubeKey).ConfigureAwait(false)));

            foreach (var info in infos.OrderByDescending(x => x.Item2.PublishedAt))
            {
                if (pages.IsEmpty || pages.Last.Embed.Fields.Count % 5 == 0)
                    pages.Add(new EmbedBuilder().WithTitle("YouTube stats"));
                
                TimeSpan timePublished = DateTime.Now.ToUniversalTime() - info.Item2.PublishedAt;

                pages.Last.Embed.AddField(eab => eab.WithName($":tv: {info.Item1.Name}").WithIsInline(false).WithValue(
                    $"**Views: **{info.Item2.Views.ToString("N0", new CultureInfo("en-US"))}\n" +
                    $"**Likes: **{info.Item2.Likes.ToString("N0", new CultureInfo("en-US"))}\n" +
                    $"**Published: **{String.Format("{0}d {1}h {2}min ago", timePublished.Days, timePublished.Hours, timePublished.Minutes)}\n\n"
                    ));
            }

            await command.Reply(Communicator, pages).ConfigureAwait(false);
        }

        private static Regex YoutubeIdRegex = new Regex(@"^[\w\-]+$|\/watch[/?].*[?&]?v=([\w\-]+)|youtu\.be\/([\w\-]+)", RegexOptions.Compiled);

        [Command("views", "add", "Adds a comeback.")]
        [Parameters(ParameterType.String, ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}views add \"ComebackName\" [-c \"CategoryName\"] YoutubeLinkOrID [MoreYoutubeLinksOrIDs]\n\nExamples:\n" +
            "{p}views add \"Starry Night\" https://www.youtube.com/watch?v=0FB2EoKTK_Q https://www.youtube.com/watch?v=LjUXm0Zy_dk\n" +
            "{p}views add \"Starry Night\" -c \"title songs\" 0FB2EoKTK_Q LjUXm0Zy_dk\n")]
        public async Task AddComeback(ICommand command)
        {
            var info = new ComebackInfo() { Name = (string)command.GetParameter(0), VideoIds = new HashSet<string>() };

            bool categoryFollows = false;
            foreach (var param in command.GetParameters().Skip(1))
            {
                if (categoryFollows)
                {
                    info.Category = (string)param;
                    categoryFollows = false;
                }
                else if ((string)param == "-c")
                    categoryFollows = true;
                else
                {
                    var match = YoutubeIdRegex.Match((string)param);
                    if (!match.Success)
                        throw new Framework.Exceptions.IncorrectParametersCommandException($"Invalid YouTube link or ID ({(string)param}).");

                    info.VideoIds.Add(match.Groups[1].Value);
                }
            }
            
            if (categoryFollows)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Expected a category name following \"-c\".");
            else if (info.VideoIds.Count <= 0)
                throw new Framework.Exceptions.IncorrectParametersCommandException("No videos specified.");

            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                s.YouTubeComebacks.Add(info);
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, "Comeback info has been added.").ConfigureAwait(false);
        }

        [Command("views", "remove", "Removes media info for a specified comeback.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}views remove \"ComebackName\" [-c \"CategoryName\"]\n\nSpecify CategoryName to remove comeback in a category, omit to delete from the default category." +
            "\n\nExamples:\n{p}views remove \"Starry Night\"\n{p}views remove \"Starry Night\" -c \"title songs\"")]
        public async Task RemoveComeback(ICommand command)
        {
            string category = null;
            if (command.ParametersCount == 3 && (string)command.GetParameter(1) == "-c")
                category = (string)command.GetParameter(2);
            else if (command.ParametersCount != 1)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Expected one parameter with comeback name and optionally a category name.");

            if (string.Compare(category, "default", true) == 0)
                category = null;

            bool anyRemoved = await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                return s.YouTubeComebacks.RemoveAll(x => string.Compare(x.Name, (string)command.GetParameter(0), true) == 0 && string.Compare(x.Category, category, true) == 0) > 0;
            }).ConfigureAwait(false);

            if (anyRemoved)
            {
                await command.ReplySuccess(Communicator, $"Comeback info has been removed.").ConfigureAwait(false);
            }
            else
            {
                await command.ReplyError(Communicator, $"Couldn't find comeback info with name `{command.Body}` in category `{category ?? "default"}`.").ConfigureAwait(false);
            }
        }

        [Command("views", "rename", "Renames a category of comeback media info.")]
        [Parameters(ParameterType.String, ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}views rename \"OriginalCategory\" \"NewCategory\"\n\nUse \"default\" to rename from/to default category.")]
        public async Task RenameComebackCategory(ICommand command)
        {
            string originalName = string.Compare((string)command.GetParameter(0), "default", true) == 0 ? null : (string)command.GetParameter(0);
            string newName = string.Compare((string)command.GetParameter(1), "default", true) == 0 ? null : (string)command.GetParameter(1);

            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                s.YouTubeComebacks.Where(x => string.Compare(x.Category, originalName, true) == 0).ForEach(x => x.Category = newName);
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, $"Moved all comebacks from {(string)command.GetParameter(0)} category.").ConfigureAwait(false);
        }

        [Command("views", "list", "Lists all comeback info.")]
        [Usage("{p}views list")]
        public async Task ListComebacks(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId, false).ConfigureAwait(false);
            if (settings == null || settings.YouTubeComebacks.Count <= 0)
            {
                await command.ReplyError(Communicator, "No comeback info has been set. Use the `views add` command.").ConfigureAwait(false);
                return;
            }

            var result = "";
            foreach (var comeback in settings.YouTubeComebacks.OrderBy(x => x.Category).Reverse())
            {
                result += $"Name: `{comeback.Name}` Category: `" +
                    (string.IsNullOrEmpty(comeback.Category) ? "default" : comeback.Category) +
                    $"` IDs: `{string.Join(" ", comeback.VideoIds)}`\n";
            }

            await command.Reply(Communicator, result).ConfigureAwait(false);
        }

        public class YoutubeInfo
        {
            public ulong Views { get; set; }
            public ulong Likes { get; set; }
            public DateTime PublishedAt { get; set; }
        }

        public async Task<YoutubeInfo> GetYoutubeInfo(IEnumerable<string> ids, string youtubeKey)
        {
            string html = string.Empty;
            string url = @"https://www.googleapis.com/youtube/v3/videos?part=statistics,snippet&id=" + String.Join(",", ids) + @"&key=" + youtubeKey;

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
                info.Views = totalViews;
                info.Likes = totalLikes;
                info.PublishedAt = firstPublishedAt;
                return info;
            }
        }
    }
}
