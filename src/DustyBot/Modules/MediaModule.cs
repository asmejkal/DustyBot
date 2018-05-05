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
        [Usage("[p]setScheduleChannel ChannelMention")]
        public async Task SetScheduleChannel(ICommand command)
        {
            var settings = await Settings.Get<IMediaSettings>(command.GuildId).ConfigureAwait(false);

            if (command.Message.MentionedChannelIds.Count <= 0)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Expected a channel mention.");

            settings.ScheduleChannel = command.Message.MentionedChannelIds.First();
            await Settings.Save(settings).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, "Schedule channel has been set.").ConfigureAwait(false);
        }

        [Command("addSchedule", "Adds a message to be used as source for the schedule.")]
        [Parameters(ParameterType.ULong)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("[p]addSchedule MessageId\n\nThe specified message will be parsed to be used by the `schedule` command. The expected format is " +
            "following:\n\n[optional text...]```[MM/DD | HH:MM] Event description\n[MM/DD | HH:MM] Another event's description```[optional text...]\n\n" + 
            "The HH:MM can be replaced with ??:?? if the event time is unknown.\nAll times in KST.")]
        public async Task AddSchedule(ICommand command)
        {
            var settings = await Settings.Get<IMediaSettings>(command.GuildId).ConfigureAwait(false);

            //Check if the message exists
            var id = (ulong)command.GetParameter(0);
            var channel = await command.Guild.GetTextChannelAsync(settings.ScheduleChannel).ConfigureAwait(false);
            if (channel == null || await channel.GetMessageAsync(id) == null)
            {
                await command.ReplyError(Communicator, "Couldn't find the specified message.").ConfigureAwait(false);
                return;
            }
            
            settings.ScheduleMessages.Add(id);
            await Settings.Save(settings).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, "Schedule message has been added.").ConfigureAwait(false);
        }

        [Command("removeSchedule", "Removes a message used as schedule source.")]
        [Parameters(ParameterType.ULong)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("[p]removeSchedule MessageId")]
        public async Task RemoveSchedule(ICommand command)
        {
            var settings = await Settings.Get<IMediaSettings>(command.GuildId).ConfigureAwait(false);

            if (settings.ScheduleMessages.Remove((ulong)command.GetParameter(0)))
            {
                await Settings.Save(settings).ConfigureAwait(false);
                await command.ReplySuccess(Communicator, $"Schedule source has been removed.").ConfigureAwait(false);
            }
            else
            {
                await command.ReplyError(Communicator, $"A message with this ID has not been registered as a schedule source.").ConfigureAwait(false);
            }
        }

        [Command("clearSchedule", "Removes all schedule sources.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("[p]clearSchedule")]
        public async Task ClearSchedule(ICommand command)
        {
            var settings = await Settings.Get<IMediaSettings>(command.GuildId).ConfigureAwait(false);
            settings.ScheduleMessages.Clear();
            await Settings.Save(settings).ConfigureAwait(false);
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
            var settings = await Settings.Get<IMediaSettings>(command.GuildId, false);
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
