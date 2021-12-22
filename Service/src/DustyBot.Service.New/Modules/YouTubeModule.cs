using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Core.Formatting;
using DustyBot.Database.Mongo.Collections.YouTube.Models;
using DustyBot.Framework.Attributes;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Interactivity;
using DustyBot.Framework.Modules;
using DustyBot.Service.Definitions;
using DustyBot.Service.Services.Log;
using Qmmands;

namespace DustyBot.Service.Modules
{
    [Name("YouTube"), Description("Tracks your artist's stats on YouTube.")]
    [Group("views")]
    public class YouTubeModule : DustyGuildModuleBase
    {
        private readonly IYouTubeService _service;

        public YouTubeModule(IYouTubeService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [Command(""), Description("Checks how your artist's songs are doing on YouTube. Songs are added by server moderators.")]
        [Remark("Use without parameters to view songs from the default category.")]
        [Remark("Use `all` to view all songs regardless of category.")]
        public async Task<CommandResult> ShowViewsAsync(
            [Description("show stats for this category or for a specific song")]
            [Remainder, Default(YouTubeSong.DefaultCategory)]
            string songOrCategoryName)
        {
            var filter = string.Compare(songOrCategoryName, "all", true) == 0 ? null : songOrCategoryName;
            var stats = await _service.GetStatisticsAsync(Context.GuildId, filter, Bot.StoppingToken);
            var recommendations = await _service.GetCategoryRecommendationsAsync(Context.GuildId, songOrCategoryName, 3, Bot.StoppingToken);

            if (!stats.Any())
            {
                return (songOrCategoryName, recommendations.Any()) switch
                {
                    (YouTubeSong.DefaultCategory, false) => Reply("No songs have been added on this server."),
                    (YouTubeSong.DefaultCategory, true) => Reply($"No songs have been added to the default category. Try categories {recommendations.WordJoinQuotedOr()}"),
                    (_, false) => Reply($"Couldn't find a song or category named `{songOrCategoryName}`."),
                    (_, true) => Reply($"Couldn't find a song or category named `{songOrCategoryName}`. Try categories {recommendations.WordJoinQuotedOr()}")
                };
            }

            string recommendation = recommendations.Any() 
                ? $"Try also {Context.Prefix}views {string.Join(", ", recommendations)} or a song name"
                : $"Try also {Context.Prefix}views song name";

            var fields = stats.OrderByDescending(x => x.Value.FirstPublishedAt).Select(x =>
            {
                return new LocalEmbedField()
                    .WithName($":tv: {x.Key.Name}")
                    .WithValue($"**Views: **{x.Value.Views.ToString("N0", CultureDefinitions.Display)}\n" +
                        $"**Likes: **{x.Value.Likes.ToString("N0", CultureDefinitions.Display)}\n" +
                        $"**Published: **{(DateTimeOffset.UtcNow - x.Value.FirstPublishedAt).SimpleFormat()}");
            });

            return Listing(fields, x => x.WithTitle("YouTube statistics").WithFooter(recommendation), 5);
        }

        [Command("add"), Description("Adds a song.")]
        [RequireAuthorContentManager]
        [Example("\"Starry Night\" https://www.youtube.com/watch?v=0FB2EoKTK_Q https://www.youtube.com/watch?v=LjUXm0Zy_dk\n")]
        [Example("titles \"Starry Night\" https://www.youtube.com/watch?v=0FB2EoKTK_Q https://www.youtube.com/watch?v=LjUXm0Zy_dk\n")]
        public async Task<CommandResult> AddSongAsync(
            [Description("if you add a song to a category, its stats will be displayed with `views CategoryName`")]
            [Default(YouTubeSong.DefaultCategory)]
            string categoryName,
            [Description("name of the song")]
            string songName,
            [Description("one or more YouTube links (stats from multiple uploads get added together)")]
            [Pattern(@"youtube\.com\/watch[/?].*[?&]?v=([\w\-]+)|youtu\.be\/([\w\-]+)")]
            params Match[] links)
        {
            var ids = links.Select(x => x.Groups[1].Value);
            await _service.AddSongAsync(Context.GuildId, categoryName, songName, ids, Bot.StoppingToken);
            return Success($"Song `{songName}` has been added to category `{categoryName}` with videos {ids.WordJoinQuoted()}.");
        }

        [Command("remove"), Description("Removes a song.")]
        [RequireAuthorContentManager]
        public async Task<CommandResult> RemoveSongAsync(
            [Description("specify to remove a song from a specific category, omit to remove it from the default category")]
            [Default(YouTubeSong.DefaultCategory)]
            string categoryName,
            [Description("name of the song")]
            [Remainder]
            string songName)
        {
            return await _service.RemoveSongAsync(Context.GuildId, categoryName, songName, Bot.StoppingToken) switch
            {
                true => Success($"Removed song `{songName}` from category `{categoryName}`."),
                false => Failure($"Couldn't find a song with name `{songName}` in category `{categoryName}`.")
            };
        }

        [Command("rename"), Description("Renames a song.")]
        [RequireAuthorContentManager]
        public async Task<CommandResult> RenameSongAsync(string oldName, string newName)
        {
            return await _service.RenameSongAsync(Context.GuildId, oldName, newName, Bot.StoppingToken) switch
            {
                true => Success($"Renamed song `{oldName}` to `{newName}`."),
                false => Failure("There are no songs with the specified name.")
            };
        }

        [Command("move"), Description("Moves all songs from one category to another.")]
        [RequireAuthorContentManager]
        [Remark($"Use `{YouTubeSong.DefaultCategory}` to move songs from/to the default category.")]
        public async Task<CommandResult> MoveCategoryAsync(
            [Description("current category")]
            string oldName,
            [Description("new category")]
            string newName)
        {
            return await _service.MoveCategoryAsync(Context.GuildId, oldName, newName, Bot.StoppingToken) switch
            {
                true => Success($"Moved all songs from category `{oldName}` to `{newName}`."),
                false => Failure("There are no songs in the specified category.")
            };
        }

        [Command("list"), Description("Lists all added songs.")]
        public async Task<CommandResult> ListSongsAsync()
        {
            var songs = await _service.GetSongsAsync(Context.GuildId, Bot.StoppingToken);
            return Table(songs.OrderBy(x => x.Category)
                .Select(x => new TableRow().Add("Name", x.Name).Add("Category", x.Category).Add("Videos", x.VideoIds)));
        }
    }
}
