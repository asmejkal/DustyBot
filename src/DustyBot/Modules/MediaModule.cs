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

        [Command("setScheduleChannel", "Sets a channel to be used a source for the schedule.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}setScheduleChannel ChannelMention")]
        public async Task SetScheduleChannel(ICommand command)
        {
            if (command.Message.MentionedChannelIds.Count <= 0)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Expected a channel mention.");

            await Settings.InterlockedModify<MediaSettings>(command.GuildId, settings =>
            {
                settings.ScheduleChannel = command.Message.MentionedChannelIds.First();
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, "Schedule channel has been set.").ConfigureAwait(false);
        }

        [Command("addSchedule", "Adds a message to be used as source for the schedule.")]
        [Parameters(ParameterType.ULong)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}addSchedule MessageId\n\nThe specified message will be parsed to be used by the `schedule` command. The expected format is " +
            "following:\n\n[optional text...]```[MM/DD | HH:MM] Event description\n[MM/DD | HH:MM] Another event's description```[optional text...]\n\n" +
            "The HH:MM can be replaced with ??:?? if the event time is unknown.\nAll times in KST.")]
        public async Task AddSchedule(ICommand command)
        {
            var settingsRead = await Settings.Read<MediaSettings>(command.GuildId).ConfigureAwait(false);
            if (settingsRead.ScheduleChannel == 0)
            {
                await command.ReplyError(Communicator, "Set a schedule channel with `setScheduleChannel` first.").ConfigureAwait(false);
                return;
            }

            //Check if the message exists
            var id = (ulong)command.GetParameter(0);
            var channel = await command.Guild.GetTextChannelAsync(settingsRead.ScheduleChannel).ConfigureAwait(false);
            if (channel == null || await channel.GetMessageAsync(id) == null)
            {
                await command.ReplyError(Communicator, "Couldn't find the specified message.").ConfigureAwait(false);
                return;
            }

            await Settings.InterlockedModify<MediaSettings>(command.GuildId, settings =>
            {
                settings.ScheduleMessages.Add(id);
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, "Schedule message has been added.").ConfigureAwait(false);
        }

        [Command("removeSchedule", "Removes a message used as schedule source.")]
        [Parameters(ParameterType.ULong)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}removeSchedule MessageId")]
        public async Task RemoveSchedule(ICommand command)
        {
            bool removed = await Settings.InterlockedModify(command.GuildId, (MediaSettings settings) =>
            {
                return settings.ScheduleMessages.Remove((ulong)command.GetParameter(0));
            }).ConfigureAwait(false);
            
            if (removed)
            {
                await command.ReplySuccess(Communicator, $"Schedule source has been removed.").ConfigureAwait(false);
            }
            else
            {
                await command.ReplyError(Communicator, $"A message with this ID has not been registered as a schedule source.").ConfigureAwait(false);
            }
        }

        [Command("clearSchedule", "Removes all schedule sources.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}clearSchedule")]
        public async Task ClearSchedule(ICommand command)
        {
            await Settings.InterlockedModify<MediaSettings>(command.GuildId, settings =>
            {
                settings.ScheduleMessages.Clear();
            }).ConfigureAwait(false);
                        
            await command.ReplySuccess(Communicator, $"Schedule has been cleared.").ConfigureAwait(false);
        }

        struct ScheduleEvent
        {
            public DateTime date;
            public bool hasTime;
            public string description;
        }

        private static Regex _scheduleLineRegex = new Regex(@"\s*\[([0-9]+)/([0-9]+)\s*\|\s*([0-9?]+):([0-9?]+)\]\s*(.*)", RegexOptions.Compiled);

        [Command("schedule", "Shows the upcoming schedule.")]
        public async Task Schedule(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId, false);
            if (settings == null || settings.ScheduleMessages.Count <= 0)
            {
                await command.ReplyError(Communicator, "No schedule has been set. Use the `addSchedule` command.").ConfigureAwait(false);
                return;
            }

            var channel = await command.Guild.GetTextChannelAsync(settings.ScheduleChannel).ConfigureAwait(false);
            if (channel == null)
            {
                await command.ReplyError(Communicator, "Cannot find the schedule channel. Provide a channel with `setScheduleChannel`.").ConfigureAwait(false);
                return;
            }

            var events = new List<ScheduleEvent>();
            foreach (var messageId in settings.ScheduleMessages)
            {
                var message = await channel.GetMessageAsync(messageId);

                var begin = message.Content.IndexOf("```");
                var end = message.Content.LastIndexOf("```");
                if (begin < 0 || end < 0)
                    continue;

                begin += 3;
                if (begin >= end)
                    continue;

                var schedule = message.Content.Substring(begin, end - begin);
                using (var reader = new StringReader(schedule))
                {
                    for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        try
                        {
                            var match = _scheduleLineRegex.Match(line);

                            if (match.Groups.Count < 6)
                                continue;

                            var newEvent = new ScheduleEvent { description = match.Groups[5].Value.Trim(),
                                hasTime = !match.Groups[3].Value.Contains('?') && !match.Groups[4].Value.Contains('?')
                            }; 

                            newEvent.date = new DateTime(DateTime.Now.Year,
                                int.Parse(match.Groups[1].Value),
                                int.Parse(match.Groups[2].Value),
                                newEvent.hasTime ? int.Parse(match.Groups[3].Value) : 23,
                                newEvent.hasTime ? int.Parse(match.Groups[4].Value) : 59, 0);

                            events.Add(newEvent);
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                }
            }

            if (events.Count <= 0)
            {
                await command.Message.Channel.SendMessageAsync("No upcoming events.").ConfigureAwait(false);
                return;
            }

            string result = "";//$"Upcoming events (full schedule in {channel.Mention}):";
            var currentTime = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"));
            foreach (var item in events.OrderBy(x => x.date).SkipWhile(x => x.date < currentTime.AddHours(-2)).TakeWhile(x => x.date < currentTime.AddDays(14)))
            {
                result += "\n" + item.date.ToString(item.hasTime ? @"`[MM\/dd | HH:mm]`" : @"`[MM\/dd | ??:??]`") + " " + item.description;
                
                if (!item.hasTime)
                {
                    if (currentTime.Date == item.date.Date)
                        result += $" `<today>`";
                }
                else
                {
                    var timeLeft = item.date - currentTime;
                    if (timeLeft <= TimeSpan.FromHours(-1))
                        result += $" `<{-timeLeft.Hours}h {-timeLeft.Minutes}min ago>`";
                    else if (timeLeft < TimeSpan.Zero)
                        result += $" `<{-timeLeft.Minutes}min ago>`";
                    else if (timeLeft < TimeSpan.FromHours(1))
                        result += $" `<in {timeLeft.Minutes}min>`";
                    else if (timeLeft < TimeSpan.FromHours(48))
                        result += $" `<in {Math.Floor(timeLeft.TotalHours)}h {timeLeft.Minutes}min>`";
                }
            }

            var embed = new EmbedBuilder()
                .WithTitle("Upcoming events")
                .WithDescription(result)
                .WithFooter($"Full schedule in #{channel.Name}")
                .WithColor(0xbe, 0xe7, 0xb6);

            await command.Message.Channel.SendMessageAsync("", false, embed.Build()).ConfigureAwait(false);
        }

        //TODO: Implement pagination in framework
        class PaginatedMessageContext
        {
            public int CurrentPage;
            public int TotalPages => Messages.Count;
            public List<Tuple<string, Embed>> Messages = new List<Tuple<string, Embed>>();

            public bool ControlledByInvoker;
            public ulong InvokerUserId;
        }

        SemaphoreSlim _paginatedMessagesLock = new SemaphoreSlim(1);
        Dictionary<ulong, PaginatedMessageContext> _paginatedMessages = new Dictionary<ulong, PaginatedMessageContext>();

        static readonly IEmote ARROW_LEFT = new Emoji("⬅");
        static readonly IEmote ARROW_RIGHT = new Emoji("➡");

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
                await command.ReplyError(Communicator, "No comeback info has been set for this category. Use the `addComeback` command.").ConfigureAwait(false);
                return;
            }

            const int PAGE_SIZE = 5;
            int finalPages = comebacks.Count / PAGE_SIZE + (comebacks.Count % PAGE_SIZE > 0 ? 1 : 0);
            var embed = new EmbedBuilder().WithTitle("YouTube stats").WithFooter($"Page 1 of {finalPages}");
            var context = new PaginatedMessageContext();
            int queued = 0;
            foreach (var comeback in comebacks)
            {
                queued++;
                var info = await GetYoutubeInfo(comeback.VideoIds).ConfigureAwait(false);

                TimeSpan timePublished = DateTime.Now.ToUniversalTime() - info.publishedAt;

                embed.AddField(eab => eab.WithName($":tv: {comeback.Name}").WithIsInline(false).WithValue(
                    $"**Views: **{info.views.ToString("N0", new CultureInfo("en-US"))}\n" +
                    $"**Likes: **{info.likes.ToString("N0", new CultureInfo("en-US"))}\n" +
                    $"**Published: **{String.Format("{0}d {1}h {2}min ago", timePublished.Days, timePublished.Hours, timePublished.Minutes)}\n\n"
                    ));

                if (queued % PAGE_SIZE == 0)
                {
                    queued = 0;
                    context.Messages.Add(Tuple.Create("", embed.Build()));
                    embed = new EmbedBuilder().WithTitle("YouTube stats").WithFooter($"Page {context.TotalPages + 1} of {finalPages}");
                }
            }

            if (queued > 0)
                context.Messages.Add(Tuple.Create("", embed.Build()));

            var result = await command.Message.Channel.SendMessageAsync(context.Messages.First().Item1, false, context.Messages.First().Item2).ConfigureAwait(false);

            if (context.TotalPages > 1)
            {
                try
                {
                    await _paginatedMessagesLock.WaitAsync();
                    _paginatedMessages.Add(result.Id, context);
                }
                finally
                {
                    _paginatedMessagesLock.Release();
                }
                
                await result.AddReactionAsync(ARROW_LEFT);
                await result.AddReactionAsync(ARROW_RIGHT);
            }
        }

        public override Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (!reaction.User.IsSpecified ||  reaction.User.Value.IsBot)
                        return;

                    //Check for page arrows
                    if (reaction.Emote.Name != ARROW_LEFT.Name && reaction.Emote.Name != ARROW_RIGHT.Name)
                        return;

                    //Lock and check if we have a page context for this message
                    PaginatedMessageContext context;
                    await _paginatedMessagesLock.WaitAsync();
                    if (!_paginatedMessages.TryGetValue(message.Id, out context))
                        return;

                    //If requested, only allow the original invoker of the command to flip pages
                    var concMessage = await message.GetOrDownloadAsync();
                    if (context.ControlledByInvoker && reaction.UserId != context.InvokerUserId)
                        return;

                    //Calculate new page index and check bounds
                    var newPage = context.CurrentPage + (reaction.Emote.Name == ARROW_LEFT.Name ? -1 : 1);
                    if (newPage < 0 || newPage >= context.TotalPages)
                    {
                        await concMessage.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                        return;
                    }

                    //Modify message
                    var newMessage = context.Messages.ElementAt(newPage);
                    await concMessage.ModifyAsync(x => { x.Content = newMessage.Item1; x.Embed = newMessage.Item2; });

                    //Update context and remove reaction
                    context.CurrentPage = newPage;
                    await concMessage.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                }
                catch (Exception)
                {
                    //Log
                }
                finally
                {
                    _paginatedMessagesLock.Release();
                }
            });

            return Task.CompletedTask;
        }

        [Command("addComeback", "Adds media info for a comeback to be used by other commands.")]
        [Parameters(ParameterType.String, ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}addComeback \"ComebackName\" [-c \"CategoryName\"] YoutubeVideoId [MoreYoutubeVideoIds]\n\nExamples:\n" +
            "{p}addComeback \"Starry Night\" 0FB2EoKTK_Q LjUXm0Zy_dk\n" +
            "{p}addComeback \"Starry Night\" -c \"title songs\" 0FB2EoKTK_Q LjUXm0Zy_dk\n")]
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
                    info.VideoIds.Add((string)param);
            }
            
            if (categoryFollows)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Expected a category name following \"-c\".");
            else if (info.VideoIds.Count <= 0)
                throw new Framework.Exceptions.IncorrectParametersCommandException("No videos specified.");

            await Settings.InterlockedModify<MediaSettings>(command.GuildId, settings =>
            {
                settings.YouTubeComebacks.Add(info);
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, "Comeback info has been added.").ConfigureAwait(false);
        }

        [Command("removeComeback", "Removes media info for a specified comeback.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}removeComeback \"ComebackName\" [-c \"CategoryName\"]\n\nSpecify CategoryName to remove comeback in a category, omit to delete from the default category." + 
            "\n\nExamples:\n{p}removeComeback \"Starry Night\"\n{p}removeComeback \"Starry Night\" -c \"title songs\"")]
        public async Task RemoveComeback(ICommand command)
        {
            string category = null;
            if (command.ParametersCount == 3 && (string)command.GetParameter(1) == "-c")
                category = (string)command.GetParameter(2);
            else if (command.ParametersCount != 1)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Expected one parameter with comeback name and optionally a category name.");

            if (string.Compare(category, "default", true) == 0)
                category = null;

            bool anyRemoved = await Settings.InterlockedModify(command.GuildId, (MediaSettings settings) =>
            {
                return settings.YouTubeComebacks.RemoveAll(x => string.Compare(x.Name, (string)command.GetParameter(0), true) == 0 && string.Compare(x.Category, category, true) == 0) > 0;
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

        [Command("renameComebackCategory", "Renames a category of comeback media info.")]
        [Parameters(ParameterType.String, ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}renameComebackCategory \"OriginalName\" \"NewName\"\n\nUse \"default\" to rename from/to default category.")]
        public async Task RenameComebackCategory(ICommand command)
        {
            string originalName = string.Compare((string)command.GetParameter(0), "default", true) == 0 ? null : (string)command.GetParameter(0);
            string newName = string.Compare((string)command.GetParameter(1), "default", true) == 0 ? null : (string)command.GetParameter(1);

            await Settings.InterlockedModify<MediaSettings>(command.GuildId, settings =>
            {
                settings.YouTubeComebacks.Where(x => string.Compare(x.Category, originalName, true) == 0).ForEach(x => x.Category = newName);
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, $"Moved all comebacks from {(string)command.GetParameter(0)} category.").ConfigureAwait(false);
        }

        [Command("listComebacks", "Lists all set comeback info.")]
        [Usage("{p}listComebacks")]
        public async Task ListComebacks(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId, false).ConfigureAwait(false);
            if (settings == null || settings.YouTubeComebacks.Count <= 0)
            {
                await command.ReplyError(Communicator, "No comeback info has been set. Use the `addComeback` command.").ConfigureAwait(false);
                return;
            }

            var result = "";
            foreach (var comeback in settings.YouTubeComebacks.OrderBy(x => x.Category).Reverse())
            {
                result += $"Name: `{comeback.Name}` Category: `" +
                    (string.IsNullOrEmpty(comeback.Category) ? "default" : comeback.Category) +
                    $"` IDs: `{string.Join(" ", comeback.VideoIds)}`\n";
            }

            await command.Message.Channel.SendMessageAsync(result).ConfigureAwait(false);
        }

        private static Regex _daumBoardLinkRegex = new Regex(@"(?:.*m.cafe.daum.net\/(.+)\/(\w+)\?.*boardType=\s*)|(?:.*cafe.daum.net\/(.+)\/bbs_list.+fldid=(\w+).*)", RegexOptions.Compiled);

        [Command("addCafeFeed", "Adds a Daum Cafe board feed."), RunAsync]
        [Parameters(ParameterType.String, ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}addCafeFeed DaumCafeBoardLink ChannelMention\n\nDaumCafeBoardLink - link to a standard Daum Cafe board section")]
        public async Task AddCafeFeed(ICommand command)
        {
            var feed = new DaumCafeFeed();
            try
            {
                var match = _daumBoardLinkRegex.Match((string)command.GetParameter(0));

                if (match.Groups.Count != 5)
                    throw new ArgumentException();

                feed.CafeId = match.Groups[1].Value;
                feed.BoardId = match.Groups[2].Value;
            }
            catch (Exception)
            {
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid Cafe board link.");
            }

            if (command.Message.MentionedChannelIds.Count < 1)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Missing target channel.");

            feed.TargetChannel = command.Message.MentionedChannelIds.First();
            feed.LastPostId = await Helpers.DaumCafeHelpers.GetLastPostId(feed.CafeId, feed.BoardId);

            await Settings.InterlockedModify<MediaSettings>(command.GuildId, settings =>
            {
                settings.DaumCafeFeeds.Add(feed);
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, $"Cafe feed has been added!").ConfigureAwait(false);
        }

        [Command("removeCafeFeed", "Removes a Daum Cafe board feed.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}removeCafeFeed FeedId\n\nRun `{p}listCafeFeeds` to see IDs for all active feeds.")]
        public async Task RemoveCafeFeed(ICommand command)
        {
            bool removed = await Settings.InterlockedModify(command.GuildId, (MediaSettings s) =>
            {
                return s.DaumCafeFeeds.RemoveAll(x => x.Id == Guid.Parse((string)command.GetParameter(0))) > 0;
            });

            if (removed)
                await command.ReplySuccess(Communicator, $"Feed has been removed.").ConfigureAwait(false);
            else
                await command.ReplyError(Communicator, $"A feed with this ID does not exist.").ConfigureAwait(false);
        }

        [Command("listCafeFeeds", "Lists all active Daum Cafe board feeds.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}listCafeFeeds")]
        public async Task ListCafeFeeds(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId);

            string result = "";
            foreach (var feed in settings.DaumCafeFeeds)
                result += $"Id: `{feed.Id}` Board: `{feed.CafeId}/{feed.BoardId}` Channel: `{feed.TargetChannel}`\n";

            if (string.IsNullOrEmpty(result))
                result = "No feeds have been set up. Use the `addCafeFeed` command.";

            await command.Message.Channel.SendMessageAsync(result);
        }

        [Command("addTweetFeed", "Soon™")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}addTweetFeed")]
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
