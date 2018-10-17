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
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;

namespace DustyBot.Modules
{
    [Module("Schedule", "Helps with tracking upcoming events.")]
    class ScheduleModule : Module
    {
        const string DateFormat = @"^(?:([0-9]{4})\/)?([0-9]{1,2})\/([0-9]{1,2})$";
        const string TimeFormat = @"^([0-9]{1,2}):([0-9]{1,2})|\?\?:\?\?$";

        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }

        public ScheduleModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }
        
        [Command("schedule", "Shows upcoming events.")]
        public async Task Schedule(ICommand command)
        {
            var settings = await Settings.Read<ScheduleSettings>(command.GuildId, false);
            if (settings == null || settings.ScheduleData.Count <= 0)
            {
                await command.ReplyError(Communicator, "No schedule has been set. Use the `schedule create` command.").ConfigureAwait(false);
                return;
            }

            var events = new List<ScheduleEvent>();
            var currentTime = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"));
            foreach (var message in settings.ScheduleData)
                events.AddRange(message.Events.SkipWhile(x => x.Date < currentTime.AddHours(-2)).TakeWhile(x => x.Date < currentTime.AddDays(14)));

            if (events.Count <= 0)
            {
                await command.Reply(Communicator, "No events planned for the next two weeks.").ConfigureAwait(false);
                return;
            }

            var channelReferences = new HashSet<string>();
            foreach (var id in settings.ScheduleData.Select(x => x.ChannelId).Distinct())
            {
                var channel = await command.Guild.GetChannelAsync(id);
                channelReferences.Add("#" + channel.Name);
            }

            string result = string.Empty;
            foreach (var item in events.OrderBy(x => x.Date))
            {
                result += "\n" + PrintedScheduleMessage.FormatEvent(item, settings.EventFormat);

                if (!item.HasTime)
                {
                    if (currentTime.Date == item.Date.Date)
                        result += $" `<today>`";
                }
                else
                {
                    var timeLeft = item.Date - currentTime;
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
                .WithFooter($"Events until {currentTime.AddDays(14).ToString(@"MM\/dd")} • Full schedule in {channelReferences.WordJoin()}")
                .WithColor(0xbe, 0xe7, 0xb6);

            await command.Message.Channel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
        }

        [Command("schedule", "create", "Creates a new editable message with schedule.")]
        [Parameter("Channel", ParameterType.TextChannel, "target channel")]
        [Parameter("Header", ParameterType.String, ParameterFlags.Optional, "header for the message")]
        [Parameter("Footer", ParameterType.String, ParameterFlags.Optional, "footer for the message")]
        [Comment("Sends an empty schedule message (e.g. to your `#schedule` channel). You can then add events with the `event add` command.")]
        public async Task CreateSchedule(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);
            var message = await PrintedScheduleMessage.CreateNew(command["Channel"].AsTextChannel, command["Header"], command["Footer"], Settings);

            bool first = await Settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                s.ScheduleData.Add(message.Data);
                return s.ScheduleData.Count <= 1;
            }).ConfigureAwait(false);

            Embed guide = null;
            if (first)
            {
                guide = new EmbedBuilder()
                    .WithTitle("Guide")
                    .WithDescription("The `schedule create` command creates an editable message that will contain all of your schedule (past and future events). It can be then edited by your moderators or users that have a special role.")
                    .AddField(x => x.WithName(":one: You may create more than one message").WithValue("Servers usually create one message for each month of schedule or one for every two-weeks etc. You can even maintain multiple different schedules."))
                    .AddField(x => x.WithName(":two: Where to place the schedule").WithValue("Usually, servers place their schedule messages in a #schedule channel or pin them in main chat or #updates."))
                    .AddField(x => x.WithName(":three: The \"schedule\" command").WithValue("The `schedule` command is for convenience. Users can use it to see events happening in the next two weeks across all schedules, with countdowns."))
                    .AddField(x => x.WithName(":four: Adding events").WithValue("Use the `event add` command to add events. The command adds events to the newest schedule message by default. If you need to add an event to a different message, put its ID as the first parameter."))
                    .Build();
            }

            await command.ReplySuccess(Communicator, $"Schedule message with ID `{message.Data.MessageId}` has been created. Use the `event add` or `event add {message.Data.MessageId}` command to add events.", guide).ConfigureAwait(false);
        }

        //[Command("schedule", "link", "Link schedule with Google Calendar.")]
        //[Parameter("MessageId", ParameterType.Id, "ID of a schedule message previously created with `schedule create`")]
        //[Parameter("CalendarId", @".+@.+", ParameterType.String)]
        //[Parameter("FromDate", DateFormat, ParameterType.Regex, "import events from this date onward; date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        //[Parameter("ToDate", DateFormat, ParameterType.Regex, ParameterFlags.Optional, "import events before this date")]
        //public async Task LinkSchedule(ICommand command)
        //{
        //    await AssertPrivileges(command.Message.Author, command.GuildId);
        //    var config = await Settings.ReadGlobal<BotConfig>();
        //    if (config.GCalendarSAC == null)
        //    {
        //        await command.ReplyError(Communicator, "Google Calendar API credentials are not set up. Contact the bot owner.");
        //        return;
        //    }

        //    var fromDate = DateTime.ParseExact(command["FromDate"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None);
        //    var toDate = command["ToDate"].HasValue ? DateTime.ParseExact(command["ToDate"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None) : DateTime.MaxValue;

        //    var message = await GetScheduleMessage(command.Guild, (ulong?)command["MessageId"]);

        //    var sac = new ServiceAccountCredential(new ServiceAccountCredential.Initializer(config.GCalendarSAC.Id).FromPrivateKey(config.GCalendarSAC.Key));
        //    var credential = GoogleCredential.FromServiceAccountCredential(sac).CreateScoped(new string[] { CalendarService.Scope.Calendar });
        //    var service = new CalendarService(new BaseClientService.Initializer() { HttpClientInitializer = credential, ApplicationName = "Dusty Bot" });

        //    string name;
        //    try
        //    {
        //        var calendar = await service.CalendarList.Insert(new CalendarListEntry() { Id = command["CalendarId"] }).ExecuteAsync();
        //        name = calendar.Summary;

        //        var tz = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        //        var events = await service.Events.List(command["CalendarId"]).ExecuteAsync();
        //        foreach (var e in events.Items)
        //        {
        //            DateTime start;
        //            if (e.Start.DateTime.HasValue)
        //                start = TimeZoneInfo.ConvertTime(e.Start.DateTime.Value, tz);
        //            else if (!string.IsNullOrEmpty(e.Start.Date))
        //            {
        //                if (!DateTime.TryParseExact(e.Start.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
        //                    continue;
        //            }
        //            else
        //                continue;

        //            if (start < fromDate || start >= toDate)
        //                continue;

        //            var scheduleEvent = new ScheduleEvent()
        //            {
        //                Date = start,
        //                HasTime = e.Start.DateTime.HasValue,
        //                Description = string.IsNullOrWhiteSpace(e.Summary) ? "Unnamed event" : e.Summary
        //            };

        //            message.Add(scheduleEvent);
        //        }

        //        await Settings.Modify(command.GuildId, (ScheduleSettings x) => x.ScheduleData.First(y => y.MessageId == message.Message.Id).GCalendarId = calendar.Id);
        //    }
        //    catch (Exception)
        //    {
        //        await command.ReplyError(Communicator, "Failed to add the calendar.");
        //        return;
        //    }

        //    try
        //    {
        //        await message.CommitChanges();
        //    }
        //    catch (ArgumentOutOfRangeException)
        //    {
        //        await command.ReplyError(Communicator, "The specified schedule message cannot contain all of the imported events, try narrowing the date range.").ConfigureAwait(false);
        //        return;
        //    }

        //    var dateExplanation = command["ToDate"].HasValue ? $"between `{fromDate.ToString(@"yyyy\/MM\/dd HH:mm")}` and `{toDate.ToString(@"yyyy\/MM\/dd HH:mm")}`" : $"older than `{fromDate.ToString(@"yyyy\/MM\/dd HH:mm")}`";
        //    await command.ReplySuccess(Communicator, $"Events from calendar `{name}` {dateExplanation} have been linked to message `{command["MessageId"].AsId}`.").ConfigureAwait(false);
        //}

        [Command("schedule", "edit", "header", "Sets a header for a schedule message.")]
        [Parameter("MessageId", ParameterType.Id, "ID of a schedule message previously created with `schedule create`")]
        [Parameter("Header", ParameterType.String, ParameterFlags.Remainder, "new header")]
        [Example("462366629247057930 Schedule (June)")]
        public async Task HeaderEditSchedule(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);
            var message = await GetScheduleMessage(command.Guild, (ulong?)command["MessageId"]);

            message.Header = command["Header"];
            await message.CommitChanges();

            await command.ReplySuccess(Communicator, $"Header has been set.").ConfigureAwait(false);
        }

        [Command("schedule", "edit", "footer", "Sets a footer for a schedule message.")]
        [Parameter("MessageId", ParameterType.Id, "ID of a schedule message previously created with `schedule create`")]
        [Parameter("Footer", ParameterType.String, ParameterFlags.Remainder, "new footer")]
        [Example("462366629247057930 Ooh, a new footer.")]
        public async Task FooterEditSchedule(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);
            var message = await GetScheduleMessage(command.Guild, (ulong?)command["MessageId"]);

            message.Footer = command["Footer"];
            await message.CommitChanges();

            await command.ReplySuccess(Communicator, $"Footer has been set.").ConfigureAwait(false);
        }

        private static readonly Regex _editEventRegex = new Regex(@"^\s*\[([0-9]+)\/([0-9]+)\s*\|\s*([0-9?]+):([0-9?]+)\]\s*(.*)$");
        [Command("schedule", "edit", "Edits the content of a schedule message.")]
        [Parameter("MessageId", ParameterType.Id, "ID of a schedule message previously created with `schedule create`")]
        [Parameter("Year", ParameterType.Int, "the year the events take place in")]
        [Parameter("Events", @"^\s*\[[0-9]+\/[0-9]+\s*\|\s*[0-9?]+:[0-9?]+\].+", ParameterType.String, ParameterFlags.Remainder, "new content")]
        [Comment("Old content will be replaced. The new content has to be in the following format:\n[08/10 | 08:00] Event 1\n[08/11 | ??:??] Event 2\n[08/12 | 10:00] Event 3\n...\n\nEach event has to be a on a new line.")]
        [Example("462366629247057930 2018\n[08/10 | 08:00] Event 1\n[08/11 | ??:??] Event 2\n[08/12 | 10:00] Event 3")]
        public async Task EditSchedule(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);
            var message = await GetScheduleMessage(command.Guild, (ulong?)command["MessageId"]);

            message.Clear();
            var content = command["Events"];
            using (var reader = new StringReader(content))
            {
                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    try
                    {
                        var match = _editEventRegex.Match(line);

                        if (!match.Success)
                            throw new Framework.Exceptions.IncorrectParametersCommandException($"Line `{line}` is invalid");

                        var newEvent = new ScheduleEvent
                        {
                            Description = match.Groups[5].Value.Trim(),
                            HasTime = !match.Groups[3].Value.Contains('?') && !match.Groups[4].Value.Contains('?')
                        };

                        newEvent.Date = new DateTime((int)command["Year"],
                            int.Parse(match.Groups[1].Value),
                            int.Parse(match.Groups[2].Value),
                            newEvent.HasTime ? int.Parse(match.Groups[3].Value) : 23,
                            newEvent.HasTime ? int.Parse(match.Groups[4].Value) : 59, 0);

                        message.Add(newEvent);
                    }
                    catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                    {
                        throw new Framework.Exceptions.IncorrectParametersCommandException($"Line `{line}` is invalid");
                    }
                }
            }

            try
            {
                await message.CommitChanges();
            }
            catch (ArgumentOutOfRangeException)
            {
                await command.ReplyError(Communicator, "The message is too long.").ConfigureAwait(false);
                return;
            }

            await command.ReplySuccess(Communicator, $"Message has been edited.").ConfigureAwait(false);
        }

        [Command("schedule", "move", "Moves a range of events from one message to another.")]
        [Parameter("SourceMessageId", ParameterType.Id, "ID of the source message (previously created with `schedule create`)")]
        [Parameter("TargetMessageId", ParameterType.Id, "ID of the target message (previously created with `schedule create`)")]
        [Parameter("FromDate", DateFormat, ParameterType.Regex, "move events from this date onward; date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Parameter("ToDate", DateFormat, ParameterType.Regex, ParameterFlags.Optional, "move events before this date")]
        [Example("462366629247057930 476740163159195649 08/09")]
        [Example("462366629247057930 476740163159195649 2018/08/09 2018/12/01")]
        public async Task MoveSchedule(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);
            var source = await GetScheduleMessage(command.Guild, (ulong?)command[0]);
            var target = await GetScheduleMessage(command.Guild, (ulong?)command[1]);
            
            var fromDate = DateTime.ParseExact(command["FromDate"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None);
            var toDate = command["ToDate"].HasValue ? DateTime.ParseExact(command["ToDate"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None) : DateTime.MaxValue;

            var moved = source.MoveAll(target, x => x.Date >= fromDate && x.Date < toDate);

            if (moved > 0)
            {
                await target.CommitChanges();
                await source.CommitChanges();
            }

            await command.ReplySuccess(Communicator, $"Moved {moved} events.").ConfigureAwait(false);
        }

        [Command("schedule", "ignore", "Stops using a schedule message.")]
        [Parameter("MessageId", ParameterType.Id)]
        [Comment("Stops using this message for the `schedule` and `event add/remove` commands.")]
        public async Task IgnoreSchedule(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);
            bool removed = await Settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                return s.ScheduleData.RemoveAll(x => x.MessageId == (ulong)command[0]) > 0;
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

        [Command("schedule", "list", "Lists all schedule messages.")]
        public async Task ListSchedule(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);
            var settings = await Settings.Read<ScheduleSettings>(command.GuildId, false).ConfigureAwait(false);
            if (settings == null || settings.ScheduleData.Count <= 0)
            {
                await command.Reply(Communicator, $"No schedule sources.").ConfigureAwait(false);
                return;
            }

            var result = new StringBuilder();
            foreach (var message in settings.ScheduleData)
            {
                result.AppendLine($"Channel: <#{message.ChannelId}> Id: `{message.MessageId}` Header: `{message.Header}`");
            }
                

            await command.Reply(Communicator, result.ToString()).ConfigureAwait(false);
        }

        [Command("schedule", "role", "Sets an optional role that allows users to edit the schedule.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("RoleNameOrID", ParameterType.Role, ParameterFlags.Optional | ParameterFlags.Remainder)]
        [Comment("Users with this role will be able to edit the schedule, in addition to users with the Manage Messages privilege.\n\nUse without parameters to disable.")]
        public async Task ScheduleRole(ICommand command)
        {
            var r = await Settings.Modify(command.GuildId, (ScheduleSettings s) => s.ScheduleRole = command["RoleNameOrID"].AsRole?.Id ?? default(ulong)).ConfigureAwait(false);
            if (r == default(ulong))
                await command.ReplySuccess(Communicator, $"Schedule management role has been disabled. Users with the Manage Messages permission can still edit the schedule.").ConfigureAwait(false);
            else
                await command.ReplySuccess(Communicator, $"Users with role `{command["RoleNameOrID"].AsRole.Id}` will now be allowed to manage the schedule.").ConfigureAwait(false);
        }

        [Command("schedule", "style", "Switches between display styles.")]
        [Parameter("EventFormat", ParameterType.String, ParameterFlags.Remainder, "style of event formatting")]
        [Comment("Changes how your schedule messages and the `schedule` command output look.\n\n**__Event formatting styles:__**\n● **Default** – embed with each event on a new line:\n`[10/06 | 13:00]` Event\n\n● **KoreanDate** – default with korean date formatting:\n`[181006 | 13:00]` Event\n\nExisting messages will switch to the new style once they are edited. If you have an idea for a style that isn't listed here, please make a suggestion on the [support server](https://discord.gg/mKKJFvZ) or contact the bot owner.")]
        [Example("KoreanDate")]
        public async Task FormatSchedule(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            if (!Enum.TryParse(command["EventFormat"], true, out EventFormat format))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Unknown event formatting type.");

            await Settings.Modify(command.GuildId, (ScheduleSettings s) => s.EventFormat = format).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, $"Schedule display style has been set to `{format}`. Existing messages will switch to the new style once they are edited.").ConfigureAwait(false);
        }

        [Command("event", "add", "Adds an event to schedule.")]
        [Parameter("MessageId", ParameterType.Id, ParameterFlags.Optional, "ID of a schedule message previously created with `schedule create`, uses the latest by default")]
        [Parameter("Date", DateFormat, ParameterType.Regex, "date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Parameter("Time", TimeFormat, ParameterType.Regex, ParameterFlags.Optional, "time in `HH:mm` format (eg. `08:45`); skip if the time is unknown")]
        [Parameter("Description", ParameterType.String, ParameterFlags.Remainder, "event description")]
        [Comment("All times in KST.")]
        [Example("07/23 08:45 Concert")]
        [Example("462366629247057930 07/23 Fansign")]
        [Example("462366629247057930 2019/01/23 Festival")]
        public async Task AddEvent(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);
            var schedule = await GetScheduleMessage(command.Guild, (ulong?)command["MessageId"]);
            ScheduleEvent e;

            try
            {
                var dateTime = DateTime.ParseExact(command["Date"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None);
                bool hasTime = command["Time"].HasValue && command["Time"].AsRegex.Groups[1].Success && command["Time"].AsRegex.Groups[2].Success;
                if (hasTime)
                    dateTime = dateTime.Add(new TimeSpan(int.Parse(command["Time"].AsRegex.Groups[1].Value), int.Parse(command["Time"].AsRegex.Groups[2].Value), 0));

                e = new ScheduleEvent
                {
                    Date = dateTime,
                    HasTime = hasTime,
                    Description = command["Description"]
                };
            }
            catch (FormatException)
            {
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid date.");
            }

            if (string.IsNullOrEmpty(e.Description))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Description is required.");

            try
            {
                schedule.Add(e);
                await schedule.CommitChanges();
            }
            catch (ArgumentOutOfRangeException)
            {
                await command.ReplyError(Communicator, "The message is too long. Create a new one with the `schedule create` command or move events with the `schedule move` command.").ConfigureAwait(false);
                return;
            }

            await command.ReplySuccess(Communicator, $"Event `{e.Description}` taking place on `{e.Date.ToString(@"yyyy\/MM\/dd")}`" + (e.HasTime ? $" at `{e.Date.ToString("HH:mm")}`" : "") + $" has been added with ID `{e.Id}`.").ConfigureAwait(false);
        }

        [Command("event", "remove", "Removes an event from schedule.")]
        [Parameter("MessageId", ParameterType.Id, ParameterFlags.Optional, "ID of a schedule message previously created with `schedule create`, uses the latest by default")]
        [Parameter("Date", DateFormat, ParameterType.Regex, ParameterFlags.Optional, "date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Parameter("Time", TimeFormat, ParameterType.Regex, ParameterFlags.Optional, "time in `HH:mm` format (eg. `08:45`)")]
        [Parameter("Description", ParameterType.String, ParameterFlags.Remainder, "event description")]
        [Comment("All times in KST.")]
        [Example("Concert")]
        [Example("462366629247057930 07/23 08:45 Festival")]
        public async Task RemoveEvent(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);
            var schedule = await GetScheduleMessage(command.Guild, (ulong?)command["MessageId"]);

            string description = command["Description"];

            if (string.IsNullOrEmpty(description))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Description is required.");

            DateTime? date;
            try
            { 
                date = command["Date"].HasValue ? new DateTime?(DateTime.ParseExact(command["Date"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None)) : null;
            }
            catch (FormatException)
            {
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid date.");
            }

            bool hasTime = command["Time"].HasValue && command["Time"].AsRegex.Groups[1].Success && command["Time"].AsRegex.Groups[2].Success;
            var removed = schedule.RemoveAll(x =>
            {
                if (string.Compare(x.Description, description, true) != 0)
                    return false;

                if (date.HasValue && (date.Value.Year != x.Date.Year || date.Value.Month != x.Date.Month || date.Value.Day != x.Date.Day))
                    return false;

                if (hasTime && (int.Parse(command["Time"].AsRegex.Groups[1].Value) != x.Date.Hour || int.Parse(command["Time"].AsRegex.Groups[2].Value) != x.Date.Minute))
                    return false;

                return true;
            });
            
            if (removed > 0)
            {
                await schedule.CommitChanges();

                if (removed > 1)
                    await command.ReplySuccess(Communicator, $"Removed {removed} events with description `{description}`.").ConfigureAwait(false);
                else
                    await command.ReplySuccess(Communicator, $"Event `{description}` has been removed.").ConfigureAwait(false);
            }
            else
            {
                await command.ReplyError(Communicator, $"Cannot find event `{description}`.").ConfigureAwait(false);
            }
        }

        [Command("event", "edit", "Edits an event in schedule.")]
        [Parameter("MessageId", ParameterType.Id, ParameterFlags.Optional, "ID of a schedule message previously created with `schedule create`, uses the latest by default")]
        [Parameter("EventId", ParameterType.Int, "ID of the event to edit; it gets printed out when an event is added")]
        [Parameter("Date", DateFormat, ParameterType.Regex, ParameterFlags.Optional, "new date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Parameter("Time", TimeFormat, ParameterType.Regex, ParameterFlags.Optional, "new time in `HH:mm` format (eg. `08:45`); use `??:??` to specify an unknown time")]
        [Parameter("Description", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "new event description")]
        [Comment("All times in KST. An alternative to this command is `event remove` followed by `event add`.")]
        [Example("5 08:45")]
        [Example("462366629247057930 13 07/23 Fansign")]
        [Example("462366629247057930 25 Festival")]
        public async Task EditEvent(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);
            var schedule = await GetScheduleMessage(command.Guild, (ulong?)command["MessageId"]);
            var e = schedule.Events.FirstOrDefault(x => x.Id == (int)command["EventId"]);
            if (e == null)
                throw new Framework.Exceptions.IncorrectParametersCommandException($"Cannot find an event with ID `{(int)command["EventId"]}` in message `{schedule.Message.Id}`.");

            try
            {
                var date = e.Date.Date;
                var time = e.Date.TimeOfDay;
                if (command["Date"].HasValue)
                    date = DateTime.ParseExact(command["Date"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None);

                bool hasTime = e.HasTime;
                if (command["Time"].HasValue)
                {
                    hasTime = command["Time"].HasValue && command["Time"].AsRegex.Groups[1].Success && command["Time"].AsRegex.Groups[2].Success;
                    if (hasTime)
                        time = new TimeSpan(int.Parse(command["Time"].AsRegex.Groups[1].Value), int.Parse(command["Time"].AsRegex.Groups[2].Value), 0);
                    else
                        time = new TimeSpan();
                }

                e.Date = date.Add(time);
                e.HasTime = hasTime;

                if (command["Description"].HasValue)
                    e.Description = command["Description"];
            }
            catch (FormatException)
            {
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid date.");
            }

            if (string.IsNullOrEmpty(e.Description))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Description is required.");

            try
            {
                await schedule.CommitChanges();
            }
            catch (ArgumentOutOfRangeException)
            {
                await command.ReplyError(Communicator, "The message is too long. Create a new one with the `schedule create` command.").ConfigureAwait(false);
                return;
            }

            await command.ReplySuccess(Communicator, $"Event `{e.Id}` has been edited to `{e.Description}` taking place on `{e.Date.ToString(@"yyyy\/MM\/dd")}`" + (e.HasTime ? $" at `{e.Date.ToString("HH:mm")}`" : "") + $".").ConfigureAwait(false);
        }

        async Task<IScheduleMessage> GetScheduleMessage(IGuild guild, ulong? messageId = null, bool mustBeEditable = true)
        {
            var settings = await Settings.Read<ScheduleSettings>(guild.Id);
            var schedule = messageId.HasValue ?
                settings.ScheduleData.FirstOrDefault(x => x.MessageId == messageId) :
                settings.ScheduleData.LastOrDefault();

            if (schedule == null)
                throw new Framework.Exceptions.IncorrectParametersCommandException(settings.ScheduleData.Any() ? "Not a registered schedule message. Use `schedule list` to see a list of all active messages." : "No schedule message has been created. Use `schedule create`.");

            var result = await PrintedScheduleMessage.Create(schedule, guild, Settings);

            if (result == null)
                throw new Framework.Exceptions.IncorrectParametersCommandException($"Cannot find the schedule message (ID `{schedule.MessageId}`).");

            if (mustBeEditable && !result.Editable)
                throw new Framework.Exceptions.IncorrectParametersCommandException("The bot cannot edit this message. You can only edit messages sent by the `schedule create` command.");

            return result;
        }

        async Task AssertPrivileges(IUser user, ulong guildId)
        {
            if (user is IGuildUser gu)
            {
                if (gu.GuildPermissions.ManageMessages)
                    return;

                var s = await Settings.Read<ScheduleSettings>(guildId);
                if (s.ScheduleRole == default(ulong) || !gu.RoleIds.Contains(s.ScheduleRole))
                    throw new Framework.Exceptions.MissingPermissionsException("You may bypass this requirement by asking for a schedule management role.", GuildPermission.ManageMessages);
            }
            else
                throw new InvalidOperationException();
        }

        interface IScheduleMessage
        {
            IReadOnlyCollection<ScheduleEvent> Events { get; }

            string Header { get; set; }
            string Footer { get; set; }

            bool Editable { get; }
            IUserMessage Message { get; }

            ScheduleData Data { get; }

            IEnumerable<ScheduleEvent> GetEvents(DateTime from, DateTime to);
            void Add(ScheduleEvent e);
            void Remove(ScheduleEvent e);
            int RemoveAll(Predicate<ScheduleEvent> predicate);
            int MoveAll(IScheduleMessage other, Predicate<ScheduleEvent> predicate);
            void Clear();

            Task CommitChanges();
        }

        class PrintedScheduleMessage : IScheduleMessage
        {
            const string DefaultHeader = ":calendar_spiral: Schedule";

            ISettingsProvider _settings;

            public ScheduleData Data { get; }

            public string Header { get => Data.Header; set => Data.Header = value; }
            public string Footer { get => Data.Footer; set => Data.Footer = value; }

            public bool Editable => Message.Author.Id == ((Message.Channel as ITextChannel)?.Guild as SocketGuild)?.CurrentUser.Id;
            public IUserMessage Message { get; }

            public IReadOnlyCollection<ScheduleEvent> Events => Data.Events as IReadOnlyCollection<ScheduleEvent>;

            private PrintedScheduleMessage(ScheduleData data, IUserMessage message, ISettingsProvider settings)
            {
                Data = data;
                Message = message;
                _settings = settings;
            }

            public static async Task<PrintedScheduleMessage> Create(ScheduleData data, IGuild guild, ISettingsProvider settings)
            {
                var channel = await guild.GetTextChannelAsync(data.ChannelId);
                var message = channel != null ? await channel.GetMessageAsync(data.MessageId) as IUserMessage : null;
                if (message == null)
                    return null;

                return new PrintedScheduleMessage(data, message, settings);
            }

            public static async Task<PrintedScheduleMessage> CreateNew(ITextChannel channel, string header, string footer, ISettingsProvider settings)
            {
                header = string.IsNullOrEmpty(header) ? DefaultHeader : header;
                var embed = new EmbedBuilder()
                    .WithTitle(header)
                    .WithDescription("")
                    .WithFooter(footer);

                var message = await channel.SendMessageAsync(string.Empty, embed: embed.Build());

                var data = new ScheduleData()
                {
                    MessageId = message.Id,
                    ChannelId = channel.Id,
                    Header = header,
                    Footer = footer
                };

                return new PrintedScheduleMessage(data, message, settings);
            }

            public IEnumerable<ScheduleEvent> GetEvents(DateTime from, DateTime to) => Events.SkipWhile(x => x.Date < from).TakeWhile(x => x.Date < to);

            public void Add(ScheduleEvent e)
            {
                if (!e.HasTime)
                    e.Date = new DateTime(e.Date.Year, e.Date.Month, e.Date.Day, 23, 59, 0, 0); //Standardize and sort at the end

                if (e.Id == default(int))
                    e.Id = Data.NextEventId++;

                if (Data.Events.Any(x => x.Id == e.Id))
                    throw new ArgumentException("Duplicate event ID.");

                Data.Events.Add(e);
            }

            public void Remove(ScheduleEvent e) => Data.Events.Remove(e);

            public int RemoveAll(Predicate<ScheduleEvent> predicate)
            {
                int count = 0;
                for (int i = Data.Events.Count - 1; i >= 0; i--)
                {
                    if (!predicate(Data.Events[i]))
                        continue;

                    Data.Events.RemoveAt(i);
                    count++;
                }

                return count;
            }

            public int MoveAll(IScheduleMessage other, Predicate<ScheduleEvent> predicate)
            {
                int count = 0;
                for (int i = Data.Events.Count - 1; i >= 0; i--)
                {
                    var e = Data.Events[i];
                    if (!predicate(e))
                        continue;

                    e.Id = default(int);
                    other.Add(e);
                    Data.Events.RemoveAt(i);
                    count++;
                }

                return count;
            }

            public void Clear() => Data.Events.Clear();

            public async Task CommitChanges()
            {
                var settings = await _settings.Read<ScheduleSettings>(((ITextChannel)Message.Channel).GuildId);

                var result = new StringBuilder();
                foreach (var e in Events)
                    result.AppendLine(FormatEvent(e, settings.EventFormat));

                if (result.Length > 2000)
                    throw new ArgumentOutOfRangeException();

                var embed = new EmbedBuilder()
                    .WithTitle(string.IsNullOrEmpty(Header) ? DefaultHeader : Header)
                    .WithDescription(result.ToString())
                    .WithFooter(Footer);

                await _settings.Modify(((ITextChannel)Message.Channel).GuildId, (ScheduleSettings s) =>
                {
                    var i = s.ScheduleData.FindIndex(x => x.MessageId == Message.Id);

                    if (i < 0)
                        throw new ArgumentException();

                    s.ScheduleData[i] = Data;
                });

                await Message.ModifyAsync(x => { x.Content = string.Empty; x.Embed = embed.Build(); });
            }

            public static string FormatEvent(ScheduleEvent e, EventFormat format)
            {
                if (format == EventFormat.KoreanDate)
                    return e.Date.ToString(e.HasTime ? @"`[yyMMdd | HH:mm]`" : @"`[yyMMdd | ??:??]`") + " " + e.Description;
                else
                    return e.Date.ToString(e.HasTime ? @"`[MM\/dd | HH:mm]`" : @"`[MM\/dd | ??:??]`") + " " + e.Description;
            }
        }
    }
}
