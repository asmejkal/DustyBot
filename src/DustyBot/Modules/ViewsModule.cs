using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
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
using DustyBot.Helpers;
using System.Text.RegularExpressions;

namespace DustyBot.Modules
{
    [Module("YouTube", "Tracks your artist's comeback stats on YouTube.")]
    class ViewsModule : Module
    {
        public ICommunicator Communicator { get; }
        public ISettingsProvider Settings { get; }

        public ViewsModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }

        [Command("views", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
        }

        [Command("views", "Checks how releases are doing on YouTube. The releases need to be added by moderators.", CommandFlags.TypingIndicator)]
        [Parameter("SongOrCategoryName", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder, "select songs from a specific category or search for a song from any category")]
        [Comment("Use without parameters to view songs from the default category.")]
        public async Task Views(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId);
            string param = string.IsNullOrWhiteSpace(command["SongOrCategoryName"]) ? null : (string)command["SongOrCategoryName"];

            var comebacks = settings.YouTubeComebacks.Where(x => string.Compare(x.Category, param, true) == 0).ToList();
            if (comebacks.Count <= 0 && !string.IsNullOrWhiteSpace(param))
                comebacks = settings.YouTubeComebacks.Where(x => x.Name.Search(param, true)).ToList();

            var config = await Settings.ReadGlobal<BotConfig>();
            if (comebacks.Count <= 0)
            {
                string rec;
                if (settings.YouTubeComebacks.Count <= 0)
                    rec = "Use the `views add` command.";
                else
                    rec = "Try " + GetOtherCategoriesRecommendation(settings, config, param, true) + ".";

                await command.ReplyError(Communicator, $"No comeback info has been set for this category or song. {rec}").ConfigureAwait(false);
                return;
            }

            //Get YT data
            var pages = new PageCollection();
            var infos = new List<Tuple<ComebackInfo, YoutubeInfo>>();
            foreach (var comeback in comebacks)
                infos.Add(Tuple.Create(comeback, await GetYoutubeInfo(comeback.VideoIds, config.YouTubeKey).ConfigureAwait(false)));

            //Compose embeds with info
            string recommendation = "Try also: " + GetOtherCategoriesRecommendation(settings, config, param, false) + ".";
            foreach (var info in infos.OrderByDescending(x => x.Item2.PublishedAt))
            {
                if (pages.IsEmpty || pages.Last.Embed.Fields.Count % 5 == 0)
                {
                    pages.Add(new EmbedBuilder().WithTitle("YouTube"));

                    if (!string.IsNullOrEmpty(recommendation))
                        pages.Last.Embed.WithFooter(recommendation);
                }                
                
                TimeSpan timePublished = DateTime.Now.ToUniversalTime() - info.Item2.PublishedAt;

                pages.Last.Embed.AddField(eab => eab.WithName($":tv: {info.Item1.Name}").WithIsInline(false).WithValue(
                    $"**Views: **{info.Item2.Views.ToString("N0", new CultureInfo("en-US"))}\n" +
                    $"**Likes: **{info.Item2.Likes.ToString("N0", new CultureInfo("en-US"))}\n" +
                    $"**Published: **{String.Format("{0}d {1}h {2}min ago", timePublished.Days, timePublished.Hours, timePublished.Minutes)}\n\n"
                    ));
            }

            await command.Reply(Communicator, pages).ConfigureAwait(false);
        }

        private string GetOtherCategoriesRecommendation(MediaSettings settings, BotConfig config, string category, bool useMarkdown)
        {
            var otherCategories = settings.YouTubeComebacks.Select(x => x.Category)
                .Where(x => x != category)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct();

            var isDefault = settings.YouTubeComebacks.Any(x => x.Category == null) && category != null;
            
            string result = isDefault ? $"{(useMarkdown ? "`" : "")}{config.CommandPrefix}views{(useMarkdown ? "`" : "")} and " : "";
            if (otherCategories.Any())
                result += $"{(useMarkdown ? "`" : "")}{config.CommandPrefix}views {string.Join($", ", otherCategories)} or a song name{(useMarkdown ? "`" : "")}";
            else
                result += $"{(useMarkdown ? "`" : "")}{config.CommandPrefix}views song name{(useMarkdown ? "`" : "")}";

            return result;
        }

        public const string YouTubeLinkFormat = @"youtube\.com\/watch[/?].*[?&]?v=([\w\-]+)|youtu\.be\/([\w\-]+)";

        [Command("views", "add", "Adds a song.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("CategoryName", YouTubeLinkFormat, true, ParameterType.String, ParameterFlags.Optional, "if you add a song to a category, its views will be displayed with `views CategoryName`")]
        [Parameter("SongName", YouTubeLinkFormat, true, ParameterType.String, "name of the song")]
        [Parameter("YouTubeLinks", YouTubeLinkFormat, ParameterType.Regex, ParameterFlags.Repeatable, "one or more song links")]
        [Comment("If you add more than one link, their stats will be added together.")]
        [Example("\"Starry Night\" https://www.youtube.com/watch?v=0FB2EoKTK_Q https://www.youtube.com/watch?v=LjUXm0Zy_dk\n")]
        [Example("titles \"Starry Night\" https://www.youtube.com/watch?v=0FB2EoKTK_Q https://www.youtube.com/watch?v=LjUXm0Zy_dk\n")]
        public async Task AddComeback(ICommand command)
        {
            var info = new ComebackInfo
            {
                Category = command["CategoryName"].HasValue ? (string)command["CategoryName"] : null,
                Name = command["SongName"],
                VideoIds = new HashSet<string>(command["YouTubeLinks"].Repeats.Select(x => Enumerable.Cast<Group>(x.AsRegex.Groups).Skip(1).First(y => !string.IsNullOrEmpty(y.Value)).Value))
            };

            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                s.YouTubeComebacks.Add(info);
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, $"Song `{info.Name}` has been added{(string.IsNullOrEmpty(info.Category) ? "" : $" to category `{info.Category}`")} with videos {info.VideoIds.WordJoinQuoted()}.").ConfigureAwait(false);
        }

        [Command("views", "remove", "Removes a song.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("CategoryName", ParameterType.String, ParameterFlags.Optional, "specify to remove comeback in a category, omit to delete from the default category")]
        [Parameter("SongName", ParameterType.String, ParameterFlags.Remainder)]
        public async Task RemoveComeback(ICommand command)
        {
            string name, category = null;
            if (command.ParametersCount == 1)
            {
                name = command[0];
            }
            else if (command.ParametersCount == 2)
            {
                category = command[0];
                name = command[1];
            }
            else
                throw new Framework.Exceptions.IncorrectParametersCommandException("Too many parameters.");            

            if (string.Compare(category, "default", true) == 0 || string.IsNullOrEmpty(category))
                category = null;

            var removed = await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                return s.YouTubeComebacks.RemoveAll(x =>
                {
                    return string.Compare(x.Name, name, true) == 0 && 
                        string.Compare(string.IsNullOrEmpty(x.Category) ? null : x.Category, category, true) == 0;
                });
            }).ConfigureAwait(false);

            if (removed > 0)
                await command.ReplySuccess(Communicator, removed > 1 ? $"Removed {removed} songs." : "Song has been removed.").ConfigureAwait(false);
            else
                await command.ReplyError(Communicator, $"Couldn't find song with name `{name}` in category `{category ?? "default"}`.").ConfigureAwait(false);
        }

        [Command("views", "rename", "Renames a category or song.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("OldName", ParameterType.String)]
        [Parameter("NewName", ParameterType.String)]
        [Comment("Use `default` to rename from/to default category.")]
        public async Task RenameComeback(ICommand command)
        {
            string originalName = string.Compare(command[0], "default", true) == 0 ? null : (string)command[0];
            string newName = string.Compare(command[1], "default", true) == 0 ? null : (string)command[1];

            var result = await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                int ccount = 0;
                foreach (var comeback in s.YouTubeComebacks.Where(x => string.Compare(x.Category, originalName, true) == 0))
                {
                    ccount++;
                    comeback.Category = newName;
                }

                int scount = 0;
                if (originalName != null && newName != null) // Only applies to categories
                {
                    foreach (var comeback in s.YouTubeComebacks.Where(x => string.Compare(x.Name, originalName, true) == 0))
                    {
                        scount++;
                        comeback.Name = newName;
                    }
                }

                return Tuple.Create(ccount, scount);
            }).ConfigureAwait(false);

            if (result.Item1 <= 0 && result.Item2 <= 0)
            {
                await command.ReplyError(Communicator, $"There's no category or song matching this name.").ConfigureAwait(false);
                return;
            }

            await command.ReplySuccess(Communicator, (result.Item1 > 0 ? $"Moved all comebacks from `{(string)command[0]}` category. " : string.Empty) + (result.Item2 > 0 ? $"Renamed all songs named `{(string)command[0]}`. " : string.Empty)).ConfigureAwait(false);
        }

        [Command("views", "list", "Lists all songs.")]
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
            string url = $"https://www.googleapis.com/youtube/v3/videos?part=statistics,snippet&id={string.Join(",", ids)}&key={youtubeKey}";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            using (HttpWebResponse response = (HttpWebResponse)(await request.GetResponseAsync().ConfigureAwait(false)))
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                var html = await reader.ReadToEndAsync().ConfigureAwait(false);
                var json = JObject.Parse(html);
                ulong totalViews = 0;
                ulong totalLikes = 0;
                DateTime firstPublishedAt = DateTime.Now;

                var items = json["items"];
                foreach (var item in items)
                {
                    var statistics = item["statistics"];
                    totalViews += (ulong)statistics["viewCount"];
                    totalLikes += (ulong?)statistics["likeCount"] ?? 0;
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
