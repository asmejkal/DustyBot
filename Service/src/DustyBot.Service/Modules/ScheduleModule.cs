using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DustyBot.Core.Collections;
using DustyBot.Core.Formatting;
using DustyBot.Core.Miscellaneous;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Commands.Parsing;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;
using DustyBot.Framework.Utility;
using DustyBot.Service.Definitions;
using DustyBot.Service.Helpers;
using DustyBot.Service.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DustyBot.Service.Modules
{
    [Module("Schedule", "Helps with tracking upcoming events – please check out the [guide](" + HelpPlaceholders.ScheduleGuideLink + ").")]
    internal sealed class ScheduleModule
    {
        private class RefreshResult
        {
            public enum Reason
            {
                Success,
                Error,
                MessageTooLong,
                Removed
            }

            public List<(ScheduleCalendar calendar, Reason reason)> Calendars { get; } = new List<(ScheduleCalendar, Reason)>();

            public RefreshResult() { }
            public RefreshResult(ScheduleCalendar calendar, Reason reason) => Calendars.Add((calendar, reason));

            public void Add(ScheduleCalendar calendar, Reason reason) => Calendars.Add((calendar, reason));
            public void Merge(RefreshResult other) => Calendars.AddRange(other.Calendars);

            public override string ToString() => ToString(true);

            public string ToString(bool showSuceeded = true)
            {
                var builder = new StringBuilder();
                if (showSuceeded)
                {
                    var numSucceeded = Calendars.Where(x => x.reason == Reason.Success).Count();
                    if (numSucceeded > 0)
                        builder.AppendLine($"Updated {numSucceeded} calendar{(numSucceeded > 1 ? "s" : string.Empty)}.");
                    else
                        builder.AppendLine();
                }
                else
                {
                    builder.AppendLine();
                }

                var unknown = Calendars.Where(x => x.reason == Reason.Error).ToList();
                if (unknown.Any())
                    builder.AppendLine($"⚠ Failed to update {unknown.Count} calendar{(unknown.Count > 1 ? "s" : string.Empty)} ({unknown.Select(x => x.calendar.MessageId).WordJoinQuoted()}).");

                foreach (var calendar in Calendars.Where(x => x.reason == Reason.MessageTooLong).Select(x => x.calendar))
                    builder.AppendLine($"⚠ Could not update calendar `{calendar.MessageId}` (`{BuildCalendarTitle(calendar)}`) because it is too long. You can split the calendar in two with the `calendar split` command.");

                foreach (var calendar in Calendars.Where(x => x.reason == Reason.Removed).Select(x => x.calendar))
                    builder.AppendLine($"ℹ Removed calendar `{calendar.MessageId}` (`{BuildCalendarTitle(calendar)}`) because the calendar message no longer exists.");

                return builder.ToString();
            }
        }

        private const string DateRegex = @"^(?:([0-9]{4})\/)?([0-9]{1,2})\/([0-9]{1,2})$";
        private const string TimeRegex = @"^(?:([0-9]{1,2}):([0-9]{1,2})|\?\?:\?\?)$";
        private const string NoneTag = "none";
        private static readonly string[] MonthFormats = new string[] { "MMMM", "MMM", "%M" };
        private static readonly string[] DateFormats = new string[] { "yyyy/M/d", "M/d" };
        private static readonly IReadOnlyCollection<string> ReservedTags = new[] { ScheduleSettings.AllTag, NoneTag, "notify" };

        private readonly ICommunicator _communicator;
        private readonly ISettingsService _settings;
        private readonly ILogger<ScheduleModule> _logger;
        private readonly IScheduleService _service;
        private readonly IFrameworkReflector _frameworkReflector;
        private readonly ICommandParser _commandParser;
        private readonly WebsiteWalker _websiteWalker;
        private readonly HelpBuilder _helpBuilder;

        public ScheduleModule(
            ICommunicator communicator, 
            ISettingsService settings,
            ILogger<ScheduleModule> logger, 
            IScheduleService service,
            IFrameworkReflector frameworkReflector,
            ICommandParser commandParser,
            WebsiteWalker websiteWalker,
            HelpBuilder helpBuilder)
        {
            _communicator = communicator;
            _settings = settings;
            _logger = logger;
            _service = service;
            _frameworkReflector = frameworkReflector;
            _commandParser = commandParser;
            _websiteWalker = websiteWalker;
            _helpBuilder = helpBuilder;
        }

        [Command("schedule", "help", "Shows a usage guide.", CommandFlags.Hidden)]
        [Alias("event", "help"), Alias("events", "help")]
        [Alias("calendar", "help"), Alias("calendars", "help")]
        [IgnoreParameters]
        public async Task ScheduleHelp(ICommand command)
        {
            await command.Reply($"Check out the quickstart guide at <{_websiteWalker.ScheduleGuideUrl}>!");
        }

        [Command("event", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("calendar")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(_helpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
        }

        [Command("schedule", "Shows upcoming events.")]
        [Parameter("Tag", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder, "use to display events with this tag; type `all` to view all events")]
        public async Task Schedule(ICommand command)
        {
            var settings = await _settings.Read<ScheduleSettings>(command.GuildId, false);
            if (settings == null || settings.Events.Count <= 0)
            {
                await command.ReplyError($"No events to display on this server. To set up a schedule check out the guide at <{_websiteWalker.ScheduleGuideUrl}>.");
                return;
            }

            var currentTime = DateTime.UtcNow.Add(settings.TimezoneOffset);
            var tag = command["Tag"].HasValue ? command["Tag"].AsString : null;
            var all = settings.Events.SkipWhile(x => x.Date < currentTime.AddHours(-2));
            var events = all
                .Where(x => x.FitsTag(tag))
                .Take(settings.UpcomingEventsDisplayLimit + 1)
                .ToList();

            bool truncateWarning = events.Count() > settings.UpcomingEventsDisplayLimit;
            if (truncateWarning)
                events = events.SkipLast().ToList();

            if (events.Count() <= 0)
            {
                await command.Reply($"No upcoming events{(tag != null ? $" with tag `{tag}`" : "")}.{(all.Any() ? " There are upcoming events with other tags; `schedule all` will display all events regardless of tags." : "")}");
                return;
            }

            var result = new StringBuilder();
            var displayed = 0;
            foreach (var item in events)
            {
                string line = FormatEvent(item, settings.EventFormat, showTag: string.Compare(tag, ScheduleSettings.AllTag, true) == 0);

                if (!item.HasTime)
                {
                    if (currentTime.Date == item.Date.Date)
                        line += " `<today>`";
                    else if (currentTime.Date.AddDays(1) == item.Date.Date)
                        line += " `<tomorrow>`";
                }
                else
                {
                    var timeLeft = item.Date - currentTime;
                    if (timeLeft <= TimeSpan.FromHours(-1))
                        line += $" `<{Math.Floor(-timeLeft.TotalHours)}h {-timeLeft.Minutes}min ago>`";
                    else if (timeLeft < TimeSpan.Zero)
                        line += $" `<{-timeLeft.Minutes}min ago>`";
                    else if (timeLeft < TimeSpan.FromHours(1))
                        line += $" `<in {timeLeft.Minutes}min>`";
                    else if (timeLeft < TimeSpan.FromHours(48))
                        line += $" `<in {Math.Floor(timeLeft.TotalHours)}h {timeLeft.Minutes}min>`";
                }

                if (result.Length + line.Length > EmbedBuilder.MaxDescriptionLength)
                {
                    truncateWarning = true;
                    break;
                }

                result.AppendLine(line);
                displayed++;
            }

            var embed = new EmbedBuilder()
                .WithTitle("Upcoming events")
                .WithDescription(result.ToString())
                .WithFooter($"{BuildUTCOffsetString(settings)}" + (truncateWarning ? $" • Shows first {displayed} events" : string.Empty))
                .WithColor(0xbe, 0xe7, 0xb6);

            await command.Reply(embed.Build());
        }

        [Command("schedule", "set", "style", "Switches between display styles.", CommandFlags.TypingIndicator)]
        [Parameter("EventFormat", ParameterType.String, ParameterFlags.Remainder, "style of event formatting")]
        [Comment("Changes how your schedule messages and the `schedule` command output look.\n\n**__Event formatting styles:__**\n● **Default** - embed with each event on a new line:\n`[10/06 | 13:00]` Event\n\n● **KoreanDate** - default with korean date formatting:\n`[181006 | 13:00]` Event\n\n● **MonthName** - default with an abbreviated month name:\n`[Oct 06 | 13:00]` Event\n\nIf you have an idea for a style that isn't listed here, please make a suggestion on the support server or use the `feedback` command!")]
        [Example("KoreanDate")]
        public async Task SetScheduleStyle(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            if (!Enum.TryParse(command["EventFormat"], true, out EventFormat format))
                throw new IncorrectParametersCommandException("Unknown event formatting type.");

            await _settings.Modify(command.GuildId, (ScheduleSettings s) => s.EventFormat = format);
            var result = await RefreshCalendars(command.Guild);
            await command.ReplySuccess($"Schedule display style has been set to `{format}`. {result}");
        }

        [Command("schedule", "set", "manager", "Sets an optional role that allows users to edit the schedule.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("RoleNameOrID", ParameterType.Role, ParameterFlags.Optional | ParameterFlags.Remainder)]
        [Comment("Users with this role will be able to edit the schedule, in addition to users with the Manage Messages privilege.\n\nUse without parameters to disable.")]
        public async Task SetScheduleRole(ICommand command)
        {
            var r = await _settings.Modify(command.GuildId, (ScheduleSettings s) => s.ScheduleRole = command["RoleNameOrID"].AsRole?.Id ?? default);
            if (r == default)
                await command.ReplySuccess($"Schedule management role has been disabled. Users with the Manage Messages permission can still edit the schedule.");
            else
                await command.ReplySuccess($"Users with role `{command["RoleNameOrID"].AsRole.Name}` (`{command["RoleNameOrID"].AsRole.Id}`) will now be allowed to manage the schedule.");
        }

        [Command("schedule", "set", "notifications", "Sets a channel for event notifications.")]
        [Alias("schedule", "set", "notification", true)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Tag", ParameterType.String, ParameterFlags.Optional, "use if you want to setup notifications for tagged events; use `all` to notify for all events, regardless of tags")]
        [Parameter("Channel", ParameterType.TextChannel, "a channel the notifications will be posted to")]
        [Parameter("RoleNameOrID", ParameterType.Role, ParameterFlags.Optional | ParameterFlags.Remainder, "use to ping a specific role with the notifications")]
        [Comment("Only one notification setting can be active per tag.")]
        public async Task SetScheduleNotifications(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var currentUser = await command.Guild.GetCurrentUserAsync();
            if (!currentUser.GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError($"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            var pingRole = command["RoleNameOrID"].AsRole;
            if (pingRole != null && !currentUser.GuildPermissions.MentionEveryone && !pingRole.IsMentionable)
            {
                await command.ReplyError($"This role can't be pinged. Please adjust the role permissions in your server and try again.");
                return;
            }

            var tag = command["Tag"].HasValue ? command["Tag"].AsString : null;
            await _settings.Modify(command.GuildId, (ScheduleSettings s) => 
            {
                s.Notifications.RemoveAll(x => ScheduleSettings.CompareTag(x.Tag, tag, ignoreAllTag: true));
                s.Notifications.Add(new NotificationSetting()
                {
                    Tag = tag,
                    Channel = command["Channel"].AsTextChannel.Id,
                    Role = pingRole?.Id ?? default
                });
            });

            await command.ReplySuccess($"Notified events {BuildTagString(tag)} will now be posted in {command["Channel"].AsTextChannel.Mention}{(command["RoleNameOrId"].HasValue ? $" and ping the `{command["RoleNameOrId"].AsRole.Name}` role" : "")}.");
        }

        [Command("schedule", "reset", "notifications", "Resets event notifications.")]
        [Alias("schedule", "reset", "notification")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Tag", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder, "reset notifications for events with this tag; omit to reset notifications for untagged events")]
        public async Task ResetScheduleNotifications(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var tag = command["Tag"].HasValue ? command["Tag"].AsString : null;
            var r = await _settings.Modify(command.GuildId, (ScheduleSettings s) => s.Notifications.RemoveAll(x => ScheduleSettings.CompareTag(x.Tag, tag, ignoreAllTag: true)));
            if (r <= 0)
            {
                await command.ReplyError($"There's no notification settings for this tag.");
                return;
            }

            await command.ReplySuccess($"Events {BuildTagString(tag)} will no longer be notified.");
        }

        [Command("schedule", "set", "timezone", "Changes the schedule's timezone.", CommandFlags.TypingIndicator)]
        [Alias("schedule", "set", "time", "zone")]
        [Parameter("Offset", @"^(?:UTC)?\+?(-)?([0-9]{1,2}):?([0-9]{1,2})?$", ParameterType.Regex, "the timezone's offset from UTC (eg. `UTC-5` or `UTC+12:30`)")]
        [Parameter("Name", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder, "you can specify a custom name for the timezone (e.g. to show `KST` instead of `UTC+9`)")]
        [Comment("The default timezone is KST (UTC+9). The times of existing events will stay correct (recalculated to the new timezone).")]
        [Example("UTC-5")]
        [Example("UTC+12:30")]
        public async Task SetScheduleTimezone(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var offset = TimeSpan.FromHours(double.Parse(command["Offset"].AsRegex.Groups[2].Value));
            if (command["Offset"].AsRegex.Groups[3].Success)
                offset += TimeSpan.FromMinutes(double.Parse(command["Offset"].AsRegex.Groups[3].Value));

            if (command["Offset"].AsRegex.Groups[1].Success)
                offset = offset.Negate();

            if (offset < TimeSpan.FromHours(-12) || offset > TimeSpan.FromHours(14))
                throw new IncorrectParametersCommandException("Unknown timezone.", false);

            var settings = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var difference = offset - s.TimezoneOffset;
                var newEvents = new SortedList<ScheduleEvent>();
                foreach (var e in s.Events)
                {
                    if (e.HasTime) // Keep whole-day events on the same date
                        e.Date = e.Date.Add(difference);

                    newEvents.Add(e);
                }

                s.Events = newEvents; // We need to do it like this because the sort order can change (because of whole day events)
                s.TimezoneOffset = offset;
                s.TimezoneName = command["Name"].HasValue ? command["Name"].AsString : default;
                return s;
            });

            var result = await RefreshCalendars(command.Guild);
            await command.ReplySuccess($"The schedule's timezone has been set to `{BuildUTCOffsetString(settings)}`. {result}");
        }

        [Command("schedule", "set", "length", "Sets the number of events displayed by the schedule command.")]
        [Parameter("MaxEvents", ParameterType.UInt, "maximum number of events to display when using the `schedule` command; default is 15")]
        [Example("10")]
        public async Task SetScheduleLength(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            if (command["MaxEvents"].AsInt < 1)
                throw new IncorrectParametersCommandException("Must be more than 0.", false);

            await _settings.Modify(command.GuildId, (ScheduleSettings s) => s.UpcomingEventsDisplayLimit = (int)command["MaxEvents"]);
            await command.ReplySuccess($"The `schedule` command will now display up to {(int)command["MaxEvents"]} events.");
        }

        [Command("schedule", "rename", "tag", "Renames a tag for all events and calendars.", CommandFlags.TypingIndicator)]
        [Parameter("CurrentTag", ParameterType.String, "the current tag name")]
        [Parameter("NewTag", ParameterType.String, "the new tag name; use `" + NoneTag + "` to remove tags instead")]
        [Example("birthday bday")]
        [Example("birthday none")]
        public async Task RenameScheduleTag(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var currentTag = command["CurrentTag"].AsString;
            var newTag = command["NewTag"].AsString;
            var remove = ScheduleSettings.CompareTag(newTag, NoneTag, true);
            if (!remove && ReservedTags.Any(x => ScheduleSettings.CompareTag(newTag, x, true)))
                throw new CommandException("This tag is reserved, please pick a different one.");

            var countEvents = 0;
            var countCalendars = 0;
            var settings = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var newEvents = new SortedList<ScheduleEvent>();
                foreach (var e in s.Events)
                {
                    if (ScheduleSettings.CompareTag(currentTag, e.Tag, ignoreAllTag: true))
                    {
                        e.Tag = remove ? null : newTag;
                        countEvents++;
                    }

                    newEvents.Add(e);
                }

                s.Events = newEvents; // We need to do it like this because the sort order can change
                
                foreach (var c in s.Calendars.Where(x => x.FitsTag(currentTag)))
                {
                    c.Tag = remove ? null : newTag;
                    countCalendars++;
                }

                return s;
            });

            if (countEvents == 0 && countCalendars == 0)
            {
                await command.ReplyError($"Found no events or calendars with tag `{currentTag}`.");
            }
            else
            {
                var result = await RefreshCalendars(command.Guild, Enumerable.Empty<DateTime>(), new[] { currentTag, remove ? null : newTag });
                await command.ReplySuccess($"Retagged {countEvents} events and {countCalendars} calendars. {result}");
                await _service.RefreshNotifications(command.GuildId, settings);
            }
        }

        [Command("schedule", "export", "Exports all events into a text file.")]
        public async Task ExportSchedule(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var settings = await _settings.Read<ScheduleSettings>(command.GuildId, false);
            if (settings == null || !settings.Events.Any())
            {
                await command.Reply("There are no events in this server's schedule.");
                return;
            }

            var serializer = new JsonSerializer() { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented };
            var events = settings.Events.Select(x => JObject.FromObject(new
            {
                Date = x.Date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Time = x.HasTime ? x.Date.ToString("HH:mm", CultureInfo.InvariantCulture) : null,
                Description = x.Description,
                Link = x.HasLink ? x.Link : null,
                Tag = x.HasTag ? x.Tag : null,
            }, serializer));

            var result = JObject.FromObject(new 
            {
                Timezone = BuildUTCOffsetString(settings),
                Events = new JArray(events)
            });

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                serializer.Serialize(writer, result);
                writer.Flush();

                stream.Seek(0, SeekOrigin.Begin);
                await command.Channel.SendFileAsync(stream, $"Schedule-{command.Guild.Name}-{DateTime.UtcNow.ToString("yyMMdd-HH-mm", CultureInfo.InvariantCulture)}.json", $"Exported {settings.Events.Count} events!");
            }
        }

        [Command("event", "add", "Adds an event to schedule.")]
        [Alias("events", "add")]
        [Parameter("Tag", ParameterType.String, ParameterFlags.Optional, "use if you want to have calendars which display only events with specific tags")]
        [Parameter("Date", DateRegex, ParameterType.Regex, "date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Parameter("Time", TimeRegex, ParameterType.Regex, ParameterFlags.Optional, "time in `HH:mm` format (eg. `08:45`); skip if the time is unknown")]
        [Parameter("Link", ParameterType.Uri, ParameterFlags.Optional, "web link to make the event clickable")]
        [Parameter("Description", ParameterType.String, ParameterFlags.Remainder, "event description")]
        [Comment("The default timezone is KST (can be changed with `schedule set timezone`).")]
        [Example("07/23 08:45 Concert")]
        [Example("07/23 Fansign")]
        [Example("2019/01/23 Festival")]
        [Example("03/22 https://channels.vlive.tv/fcd4b V Live broadcast")]
        [Example("birthday 02/21 00:00 Solar's birthday")]
        public async Task AddEvent(ICommand command)
        {
            var (e, message) = await AddEventInner(command);

            var result = await RefreshCalendars(command.Guild, e.Date, e.Tag);
            await command.ReplySuccess(message + $" {result}");
        }

        [Command("event", "add", "notify", "Adds a notified event to schedule.")]
        [Alias("event", "notify"), Alias("events", "add", "notify"), Alias("events", "notify")]
        [Parameter("Tag", ParameterType.String, ParameterFlags.Optional, "use if you want to have calendars which display only events with specific tags")]
        [Parameter("Date", DateRegex, ParameterType.Regex, "date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Parameter("Time", TimeRegex, ParameterType.Regex, ParameterFlags.Optional, "time in `HH:mm` format (eg. `08:45`); skip if the time is unknown")]
        [Parameter("Link", ParameterType.Uri, ParameterFlags.Optional, "web link to make the event clickable")]
        [Parameter("Description", ParameterType.String, ParameterFlags.Remainder, "event description")]
        [Comment("When events added with this command begin, it will get announced in the channel set by `schedule set notifications`. \nThe default timezone is KST (can be changed with `schedule set timezone`).")]
        [Example("07/23 08:45 Concert")]
        [Example("07/23 Fansign")]
        [Example("2019/01/23 Festival")]
        [Example("03/22 https://channels.vlive.tv/fcd4b V Live broadcast")]
        [Example("birthday 02/21 00:00 Solar's birthday")]
        public async Task AddNotificationEvent(ICommand command)
        {
            var (e, message) = await AddEventInner(command, true);

            var result = await RefreshCalendars(command.Guild, e.Date, e.Tag);
            await command.ReplySuccess(message + $" {result}");
        }

        [Command("event", "remove", "Removes an event from schedule, by ID or search.")]
        [Alias("events", "remove")]
        [Parameter("IdOrSearchString", ParameterType.String, ParameterFlags.Remainder, "the event's ID or a part of description (you will be asked to choose one if multiple events match the description)")]
        [Example("13")]
        [Example("Osaka concert")]
        public async Task RemoveEvent(ICommand command)
        {
            var (e, message) = await RemoveEventInner(command);

            var result = await RefreshCalendars(command.Guild, e.Date, e.Tag);
            await command.ReplySuccess(message + $" {result}");
        }

        [Command("event", "edit", "Edits an event in schedule.")]
        [Alias("events", "edit")]
        [Parameter("EventId", ParameterType.Int, "ID of the event to edit (shown when an event is added or in `event search` and `event list`)")]
        [Parameter("Notification", @"^(?:notify|silence)$", ParameterFlags.Optional, "enter `notify` to make this a notified event or `silence` to make it a regular event")]
        [Parameter("Date", DateRegex, ParameterType.Regex, ParameterFlags.Optional, "new date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Parameter("Time", TimeRegex, ParameterType.Regex, ParameterFlags.Optional, "new time in `HH:mm` format (eg. `08:45`); use `??:??` to specify an unknown time")]
        [Parameter("Nolink", @"^nolink$", ParameterFlags.Optional | ParameterFlags.Hidden)]
        [Parameter("Link", ParameterType.Uri, ParameterFlags.Optional, "web link to make the event clickable; put `nolink` to remove a link")]
        [Parameter("Description", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "new event description")]
        [Comment("All parameters are optional – you can specify just the parts you wish to be edited (date, time, link, and/or description). The parts you leave out will stay the same.")]
        [Example("5 08:45")]
        [Example("13 07/23 18:00 Fansign")]
        [Example("25 22:00 Festival")]
        public async Task EditEvent(ICommand command)
        {
            var (origDate, e, message) = await EditEventInner(command);

            var result = await RefreshCalendars(command.Guild, new[] { origDate, e.Date }, e.Tag);
            await command.ReplySuccess(message + $" {result}");
        }

        [Command("event", "tag", "Tags an event.")]
        [Alias("events", "tag")]
        [Parameter("EventId", ParameterType.Int, "ID of the event to tag (shown when an event is added or in `event search` and `event list`)")]
        [Parameter("Tag", ParameterType.String, ParameterFlags.Remainder, "the tag; use `" + NoneTag + "` to remove a tag")]
        [Comment("Tagged events only appear in calendars that were created for that tag. \nWith the `schedule` command, tagged events appear only if the tag is specified (e.g. with `schedule <tag>`) or with `schedule all`.")]
        [Example("5 birthday")]
        [Example("31 none")]
        public async Task TagEvent(ICommand command)
        {
            var (originalTag, e, message) = await TagEventInner(command);

            var result = await RefreshCalendars(command.Guild, new[] { e.Date }, new[] { e.Tag, originalTag });
            await command.ReplySuccess(message + $" {result}");
        }

        [Command("event", "batch", "Perform multiple event add/remove/edit/tag operations at once.", CommandFlags.TypingIndicator)]
        [Alias("events", "batch")]
        [Parameter("Batch", ParameterType.String, ParameterFlags.Remainder, "a batch of `add/remove/edit/tag` commands to be executed in order; please see the example")]
        [Comment("To see the syntax for the individual commands, please see their respective help sections (`event add`, `event remove`, `event edit`, and `event tag`).")]
        [Example("\nadd 07/23 08:45 Concert\nremove 4th anniversary celebration\nedit 4 8:15\nadd birthday 02/21 00:00 Solar's birthday")]
        public async Task BatchEvent(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var commands = _frameworkReflector.GetModuleInfo(GetType()).Commands;
            var allowedCommands = new List<CommandInfo>()
            {
                commands.First(x => x.PrimaryUsage.InvokeUsage == "event add"),
                commands.First(x => x.PrimaryUsage.InvokeUsage == "event add notify"),
                commands.First(x => x.PrimaryUsage.InvokeUsage == "event remove"),
                commands.First(x => x.PrimaryUsage.InvokeUsage == "event edit"),
                commands.First(x => x.PrimaryUsage.InvokeUsage == "event tag"),
            };

            using (var reader = new StringReader(command["Batch"]))
            {
                var lineNum = 0;
                var message = new StringBuilder();
                var updates = new Dictionary<NullObject<string>, HashSet<DateTime>>();
                try
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNum++;
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        line = line.TrimStart();
                        if (line.StartsWith("event"))
                            line = command.Prefix + line;
                        else if (!line.StartsWith(command.Prefix + "event"))
                            line = command.Prefix + "event " + line;

                        var matchResult = _commandParser.Match(line, command.Prefix, allowedCommands);
                        if (matchResult == null)
                            throw new IncorrectParametersCommandException($"Unknown command. Only {allowedCommands.Select(x => x.PrimaryUsage.InvokeUsage).WordJoinQuoted()} commands are allowed.");

                        var (partialCommandRegistration, usage) = matchResult.Value;
                        var parseResult = await _commandParser.Parse(new UserMessageAdapter(command.Message) { Content = line }, partialCommandRegistration, usage, command.Prefix);
                        if (parseResult.Type != CommandParseResultType.Success)
                            throw new IncorrectParametersCommandException("");

                        var partialCommand = _commandParser.Create((SuccessCommandParseResult)parseResult);
                        IEnumerable<DateTime> updateDates;
                        var updateTags = new List<string>();
                        var verbs = partialCommandRegistration.PrimaryUsage.Verbs;
                        if (verbs.First() == "add")
                        {
                            var (e, partialMessage) = await AddEventInner(partialCommand, verbs.Skip(1).FirstOrDefault() == "notify");
                            message.AppendLine(partialMessage);
                            updateDates = new[] { e.Date };
                            updateTags.Add(e.Tag);
                        }
                        else if (verbs.First() == "notify")
                        {
                            var (e, partialMessage) = await AddEventInner(partialCommand, true);
                            message.AppendLine(partialMessage);
                            updateDates = new[] { e.Date };
                            updateTags.Add(e.Tag);
                        }
                        else if (verbs.First() == "remove")
                        {
                            var (e, partialMessage) = await RemoveEventInner(partialCommand);
                            message.AppendLine(partialMessage);
                            updateDates = new[] { e.Date };
                            updateTags.Add(e.Tag);
                        }
                        else if (verbs.First() == "edit")
                        {
                            var (origDate, e, partialMessage) = await EditEventInner(partialCommand);
                            message.AppendLine(partialMessage);
                            updateDates = new[] { origDate, e.Date };
                            updateTags.Add(e.Tag);
                        }
                        else if (verbs.First() == "tag")
                        {
                            var (originalTag, e, partialMessage) = await TagEventInner(partialCommand);
                            message.AppendLine(partialMessage);
                            updateDates = new[] { e.Date };
                            updateTags.AddRange(new[] { e.Tag, originalTag });
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unexpected command verb '{partialCommandRegistration.PrimaryUsage.Verbs.First()}'");
                        }

                        foreach (var updateTag in updateTags)
                        {
                            if (!updates.TryGetValue(updateTag, out var dates))
                                updates.Add(updateTag, dates = new HashSet<DateTime>());

                            dates.UnionWith(updateDates);
                        }
                    }
                }
                catch (Exception)
                {
                    // Update calendars for the operations that went through and report the error
                    var partialResult = new RefreshResult();
                    foreach (var (tag, dates) in updates)
                        partialResult.Merge(await RefreshCalendars(command.Guild, dates, tag.Item));

                    var resultMessage = partialResult.ToString();
                    if (!string.IsNullOrWhiteSpace(resultMessage))
                        message.AppendLine(resultMessage);

                    message.AppendLine($"An error encountered on line {lineNum}:");
                    await command.Reply("**Batch finished with errors:**\n" + message.ToString());
                    throw;
                }

                var result = new RefreshResult();
                foreach (var (tag, dates) in updates)
                    result.Merge(await RefreshCalendars(command.Guild, dates, tag.Item));

                message.AppendLine(result.ToString());
                await command.ReplySuccess("Batch finished:\n" + message.ToString());
            }
        }

        [Command("event", "search", "Searches for events and shows their IDs.")]
        [Alias("events", "search")]
        [Parameter("SearchString", ParameterType.String, ParameterFlags.Remainder, "a part of event's description; finds all events containing this string")]
        [Example("Osaka")]
        public async Task SearchEvent(ICommand command)
        {
            var settings = await _settings.Read<ScheduleSettings>(command.GuildId);
            var searchString = command["SearchString"].AsString;
            var pages = BuildEventList(settings, "Search results", predicate: x => x.Description.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0);
            if (pages.Count <= 0)
            {
                await command.Reply($"No events found containing `{searchString}` in their description.");
                return;
            }

            await command.Reply(pages);
        }

        [Command("event", "list", "Lists all events in a specified month.")]
        [Alias("events", "list")]
        [Parameter("Month", ParameterType.String, "list events from this month (use english month name or number)")]
        [Parameter("Year", ParameterType.UInt, ParameterFlags.Optional, "the month's year, uses current year by default")]
        [Example("February")]
        [Example("Feb")]
        [Example("Mar 2019")]
        public async Task ListEvents(ICommand command)
        {
            if (!DateTime.TryParseExact(command["Month"], MonthFormats, GlobalDefinitions.Culture, DateTimeStyles.None, out var month))
                throw new IncorrectParametersCommandException("Unrecognizable month name.");

            var settings = await _settings.Read<ScheduleSettings>(command.GuildId);
            var beginDate = new DateTime(command["Year"].HasValue ? (int)command["Year"] : DateTime.UtcNow.Add(settings.TimezoneOffset).Year, month.Month, 1);
            var endDate = beginDate.AddMonths(1);

            var pages = BuildEventList(settings, $"All events in {beginDate.ToString("MMMM yyyy", GlobalDefinitions.Culture)}", beginDate, endDate);
            if (pages.Count <= 0)
            {
                await command.Reply($"No events have been added for {beginDate.ToString("MMMM yyyy", GlobalDefinitions.Culture)}.");
                return;
            }

            await command.Reply(pages);
        }

        [Command("calendar", "create", "Creates a permanent message to display events.")]
        [Parameter("Tag", ParameterType.String, ParameterFlags.Optional, "display only events marked with this tag; if omitted, displays only untagged events; specify `all` to display all events, regardless of tags")]
        [Parameter("Channel", ParameterType.TextChannel, "target channel")]
        [Parameter("FromDate", DateRegex, ParameterType.Regex, "display events from this date onward (inclusive); date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Parameter("ToDate", DateRegex, ParameterType.Regex, ParameterFlags.Optional, "display only events up to this this date (inclusive)")]
        [Example("#schedule 08/01 08/14")]
        [Example("#schedule 2018/01/01")]
        [Example("birthday #schedule 2018/01/01 2018/12/31")]
        public async Task CreateCalendar(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            try
            {
                var fromDate = DateTime.ParseExact(command["FromDate"], DateFormats, GlobalDefinitions.Culture, DateTimeStyles.None);
                var toDate = command["ToDate"].HasValue ? DateTime.ParseExact(command["ToDate"], DateFormats, GlobalDefinitions.Culture, DateTimeStyles.None).AddDays(1) : DateTime.MaxValue;

                if (toDate <= fromDate)
                    throw new IncorrectParametersCommandException("The begin date has to be earlier than the end date.");

                var settings = await _settings.Read<ScheduleSettings>(command.GuildId);
                var calendar = new RangeScheduleCalendar()
                {
                    Tag = command["Tag"].HasValue ? command["Tag"].AsString : null,
                    ChannelId = command["Channel"].AsTextChannel.Id,
                    BeginDate = fromDate,
                    EndDate = toDate
                };

                try
                { 
                    var (text, embed) = BuildCalendarMessage(calendar, settings);
                    var messages = await _communicator.SendMessage(command["Channel"].AsTextChannel, text, embed);
                    calendar.MessageId = messages.Single().Id;
                }
                catch (ArgumentOutOfRangeException)
                {
                    throw new CommandException($"Could not create the calendar because it would be too long to display. Try narrowing the range of dates for the calendar.");
                }

                await _settings.Modify(command.GuildId, (ScheduleSettings s) => s.Calendars.Add(calendar));

                await command.ReplySuccess($"A calendar has been created to display events {BuildCalendarSpanString(calendar)}{BuildCalendarTagString(calendar)} in {command["Channel"].AsTextChannel.Mention}.");
            }
            catch (FormatException)
            {
                throw new IncorrectParametersCommandException("Invalid date.", false);
            }
        }

        [Command("calendar", "create", "month", "Creates a permanent message to display events in a specific month.")]
        [Parameter("Tag", ParameterType.String, ParameterFlags.Optional, "display only events marked with this tag; if omitted, displays only untagged events; specify `all` to display all events, regardless of tags")]
        [Parameter("Channel", ParameterType.TextChannel, "target channel")]
        [Parameter("Month", ParameterType.String, "display only events taking place in this month (eg. `January`)")]
        [Parameter("Year", ParameterType.UInt, ParameterFlags.Optional, "the month's year, uses current year by default")]
        [Example("#schedule February")]
        [Example("#schedule Jan 2019")]
        [Example("birthday #schedule March")]
        public async Task CreateMonthCalendar(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            if (!DateTime.TryParseExact(command["Month"], MonthFormats, GlobalDefinitions.Culture, DateTimeStyles.None, out var month))
                throw new IncorrectParametersCommandException("Unrecognizable month name.");

            var settings = await _settings.Read<ScheduleSettings>(command.GuildId);
            var beginDate = new DateTime(command["Year"].HasValue ? (int)command["Year"] : DateTime.UtcNow.Add(settings.TimezoneOffset).Year, month.Month, 1);
            var calendar = new RangeScheduleCalendar()
            {
                Tag = command["Tag"].HasValue ? command["Tag"].AsString : null,
                ChannelId = command["Channel"].AsTextChannel.Id,
                BeginDate = beginDate,
                EndDate = beginDate.AddMonths(1)
            };

            try
            { 
                var (text, embed) = BuildCalendarMessage(calendar, settings);
                var messages = await _communicator.SendMessage(command["Channel"].AsTextChannel, text, embed);
                calendar.MessageId = messages.Single().Id;
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new CommandException($"Could not create the calendar because it would be too long to display. Try narrowing the range of dates for the calendar.");
            }

            await _settings.Modify(command.GuildId, (ScheduleSettings s) => s.Calendars.Add(calendar));

            await command.ReplySuccess($"A calendar has been created to display the `{calendar.BeginDate.ToString(@"MMMM", GlobalDefinitions.Culture)}` schedule{BuildCalendarTagString(calendar, "for events")} in {command["Channel"].AsTextChannel.Mention}.");
        }

        [Command("calendar", "create", "upcoming", "Creates a permanent message to display only upcoming events.")]
        [Parameter("Tag", ParameterType.String, ParameterFlags.Optional, "display only events marked with this tag; if omitted, displays only untagged events; specify `all` to display all events, regardless of tags")]
        [Parameter("Channel", ParameterType.TextChannel, "target channel")]
        [Parameter("Days", ParameterType.UInt, ParameterFlags.Optional, "display events up to this many days in the future; if ommited, displays as many as possible")]
        [Comment("This calendar gets automatically updated every day at 0:00 (in your schedule's timezone) to show only events happening from that day onwards.")]
        [Example("#schedule")]
        [Example("#schedule 14")]
        [Example("birthday #schedule")]
        public async Task CreateUpcomingSpanCalendar(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var calendar = new UpcomingSpanScheduleCalendar()
            {
                Tag = command["Tag"].HasValue ? command["Tag"].AsString : null,
                ChannelId = command["Channel"].AsTextChannel.Id,
                DaysSpan = command["Days"].HasValue ? command["Days"].AsInt.Value : 0
            };

            var settings = await _settings.Read<ScheduleSettings>(command.GuildId);
            var (text, embed) = BuildCalendarMessage(calendar, settings);
            var messages = await _communicator.SendMessage(command["Channel"].AsTextChannel, text, embed);

            calendar.MessageId = messages.Single().Id;
            await _settings.Modify(command.GuildId, (ScheduleSettings s) => s.Calendars.Add(calendar));

            await command.ReplySuccess($"A calendar has been created to display upcoming{(calendar.DaysSpan > 0 ? $" {calendar.DaysSpan} days of" : string.Empty)} schedule{BuildCalendarTagString(calendar, "for events")} in {command["Channel"].AsTextChannel.Mention}.");
        }

        [Command("calendar", "create", "upcoming", "week", "Creates a permanent message to display events in the next 7 days.")]
        [Parameter("Tag", ParameterType.String, ParameterFlags.Optional, "display only events marked with this tag; if omitted, displays only untagged events; specify `all` to display all events, regardless of tags")]
        [Parameter("Channel", ParameterType.TextChannel, "target channel")]
        [Comment("This calendar gets automatically updated every day at 0:00 (in your schedule's timezone).")]
        [Example("#schedule")]
        [Example("birthday #schedule")]
        public async Task CreateUpcomingWeekCalendar(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var calendar = new UpcomingWeekScheduleCalendar()
            {
                Tag = command["Tag"].HasValue ? command["Tag"].AsString : null,
                ChannelId = command["Channel"].AsTextChannel.Id
            };

            var settings = await _settings.Read<ScheduleSettings>(command.GuildId);
            var (text, embed) = BuildCalendarMessage(calendar, settings);
            var messages = await _communicator.SendMessage(command["Channel"].AsTextChannel, text, embed);

            calendar.MessageId = messages.Single().Id;
            await _settings.Modify(command.GuildId, (ScheduleSettings s) => s.Calendars.Add(calendar));

            await command.ReplySuccess($"A calendar has been created to display the upcoming week of schedule{BuildCalendarTagString(calendar, "for events")} in {command["Channel"].AsTextChannel.Mention}.");
        }

        [Command("calendar", "set", "begin", "Moves the begin date of a calendar.")]
        [Parameter("MessageId", ParameterType.Id, "message ID of the calendar; use `calendar list` to see all active calendars and their IDs")]
        [Parameter("Date", DateRegex, ParameterType.Regex, "display events from this date onward (inclusive); date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Example("462366629247057930 12/01")]
        public async Task SetCalendarBegin(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            if (!DateTime.TryParseExact(command["Date"], DateFormats, GlobalDefinitions.Culture, DateTimeStyles.None, out var from))
                throw new IncorrectParametersCommandException("Invalid date.", false);

            var (calendar, settings) = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var c = GetCalendarOfType<RangeScheduleCalendar>(s, command["MessageId"].AsId.Value);
                if (c.EndDate <= from)
                    throw new IncorrectParametersCommandException("The begin date has to be earlier than the end date.");

                c.BeginDate = from;
                return (c, s);
            });

            var result = new RefreshResult(calendar, await RefreshCalendar(calendar, command.Guild, settings));
            await command.ReplySuccess($"Calendar `{calendar.MessageId}` will now display events {BuildCalendarSpanString(calendar)}. {result}");
        }

        [Command("calendar", "set", "end", "Moves the end date of a calendar.")]
        [Parameter("MessageId", ParameterType.Id, "message ID of the calendar; use `calendar list` to see all active calendars and their IDs")]
        [Parameter("Date", DateRegex, ParameterType.Regex, "display only events up to this this date (inclusive); date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Example("462366629247057930 03/31")]
        public async Task SetCalendarEnd(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            if (!DateTime.TryParseExact(command["Date"], DateFormats, GlobalDefinitions.Culture, DateTimeStyles.None, out var date))
                throw new IncorrectParametersCommandException("Invalid date.", false);

            date = date.AddDays(1); // End boundary is exclusive internally: <begin, end)

            var (calendar, settings) = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var c = GetCalendarOfType<RangeScheduleCalendar>(s, command["MessageId"].AsId.Value);
                if (date <= c.BeginDate)
                    throw new IncorrectParametersCommandException("The end date has to be later than the begin date.");

                c.EndDate = date;
                return (c, s);
            });

            var result = new RefreshResult(calendar, await RefreshCalendar(calendar, command.Guild, settings));
            await command.ReplySuccess($"Calendar `{calendar.MessageId}` will now display events {BuildCalendarSpanString(calendar)}. {result}");
        }

        [Command("calendar", "set", "month", "Sets an existing calendar to display a different month.")]
        [Parameter("MessageId", ParameterType.Id, "message ID of the calendar; use `calendar list` to see all active calendars and their IDs")]
        [Parameter("Month", ParameterType.String, "display only events taking place in this month (eg. `January`)")]
        [Parameter("Year", ParameterType.UInt, ParameterFlags.Optional, "the month's year, uses current year by default")]
        [Example("462366629247057930 February")]
        [Example("462366629247057930 Jan 2019")]
        public async Task SetCalendarMonth(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            if (!DateTime.TryParseExact(command["Month"], MonthFormats, GlobalDefinitions.Culture, DateTimeStyles.None, out var month))
                throw new IncorrectParametersCommandException("Unrecognizable month name.");

            var (calendar, settings) = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var c = GetCalendarOfType<RangeScheduleCalendar>(s, command["MessageId"].AsId.Value);

                var beginDate = new DateTime(command["Year"].HasValue ? (int)command["Year"] : DateTime.UtcNow.Add(s.TimezoneOffset).Year, month.Month, 1);
                c.BeginDate = beginDate;
                c.EndDate = beginDate.AddMonths(1);
                return (c, s);
            });

            var result = new RefreshResult(calendar, await RefreshCalendar(calendar, command.Guild, settings));
            await command.ReplySuccess($"Calendar `{calendar.MessageId}` will now display the `{calendar.BeginDate.ToString(@"MMMM", GlobalDefinitions.Culture)}` schedule. {result}");
        }
        
        [Command("calendar", "set", "title", "Sets a custom title for a calendar.")]
        [Parameter("MessageId", ParameterType.Id, "message ID of the calendar; use `calendar list` to see all active calendars and their IDs")]
        [Parameter("Title", ParameterType.String, ParameterFlags.Remainder, "new header")]
        [Example("462366629247057930 A special calendar")]
        public async Task SetCalendarTitle(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var (calendar, settings) = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var c = GetCalendarOfType<ScheduleCalendar>(s, command["MessageId"].AsId.Value);
                c.Title = command["Title"];
                return (c, s);
            });

            var result = new RefreshResult(calendar, await RefreshCalendar(calendar, command.Guild, settings));
            await command.ReplySuccess($"New title has been set.");
        }

        [Command("calendar", "set", "footer", "Sets a custom footer for a calendar.")]
        [Parameter("MessageId", ParameterType.Id, "message ID of the calendar; use `calendar list` to see all active calendars and their IDs")]
        [Parameter("Footer", ParameterType.String, ParameterFlags.Remainder, "new footer")]
        [Comment("Not recommended for calendars created with `calendar create upcoming`, as they have footers with useful dynamic information.")]
        [Example("462366629247057930 A new footer")]
        public async Task SetCalendarFooter(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var (calendar, settings) = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var c = GetCalendarOfType<ScheduleCalendar>(s, command["MessageId"].AsId.Value);
                c.Footer = command["Footer"];
                return (c, s);
            });

            var result = new RefreshResult(calendar, await RefreshCalendar(calendar, command.Guild, settings));
            await command.ReplySuccess($"New footer has been set.");
        }

        [Command("calendar", "set", "tag", "Makes a calendar display only events with a specific tag.")]
        [Parameter("MessageId", ParameterType.Id, "message ID of the calendar; use `calendar list` to see all active calendars and their IDs")]
        [Parameter("Tag", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "display only events marked with this tag; if omitted, displays only untagged events; specify `all` to display all events, regardless of tags")]
        [Example("462366629247057930 birthday")]
        [Example("462366629247057930")]
        [Example("462366629247057930 all")]
        public async Task SetCalendarTag(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var (calendar, settings) = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var c = GetCalendarOfType<ScheduleCalendar>(s, command["MessageId"].AsId.Value);
                c.Tag = command["Tag"].HasValue ? command["Tag"].AsString : null;
                return (c, s);
            });

            var result = new RefreshResult(calendar, await RefreshCalendar(calendar, command.Guild, settings));

            if (calendar.HasAllTag)
                await command.ReplySuccess($"Calendar `{calendar.MessageId}` will now display all events, regardless of tags.");
            else if (calendar.HasTag)
                await command.ReplySuccess($"Calendar `{calendar.MessageId}` will now display only events marked with tag `{calendar.Tag}`.");
            else
                await command.ReplySuccess($"Calendar `{calendar.MessageId}` will now display only untagged events.");
        }

        [Command("calendar", "list", "Lists all active calendars on this server.")]
        [Alias("calendars", "list")]
        public async Task ListCalendars(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var settings = await _settings.Read<ScheduleSettings>(command.GuildId, false);
            if (settings == null || settings.Calendars.Count <= 0)
            {
                await command.Reply($"No calendars found. Use one of the `calendar create` commands to create a calendar.");
                return;
            }

            var calendars = settings.Calendars
                .Where(x => !(x is RangeScheduleCalendar))
                .Concat(settings.Calendars.OfType<RangeScheduleCalendar>().OrderBy(x => x.BeginDate));

            var result = new StringBuilder();
            foreach (var calendar in calendars)
            {
                result.Append($"Id: `{calendar.MessageId}` Channel: <#{calendar.ChannelId}> Title: `{BuildCalendarTitle(calendar)}`");
                if (calendar is RangeScheduleCalendar rangeCalendar)
                {
                    if (rangeCalendar.IsMonthCalendar)
                        result.Append($" Month: `{rangeCalendar.BeginDate.ToString("MMMM", GlobalDefinitions.Culture)}`");
                    else
                        result.Append($" Begin: `{rangeCalendar.BeginDate.ToString(@"yyyy\/MM\/dd", GlobalDefinitions.Culture)}`" + (rangeCalendar.HasEndDate ? $" End: `{rangeCalendar.EndDate.AddDays(-1).ToString(@"yyyy\/MM\/dd", GlobalDefinitions.Culture)}` (inclusive)" : string.Empty));
                }
                else if (calendar is UpcomingSpanScheduleCalendar upcomingSpanCalendar)
                {
                    result.Append($" Upcoming: `{(upcomingSpanCalendar.DaysSpan > 0 ? $"{upcomingSpanCalendar.DaysSpan} days" : "all")}`");
                }
                else if (calendar is UpcomingWeekScheduleCalendar upcomingWeekCalendar)
                {
                    result.Append($" Upcoming: `Week`");
                }
                else
                {
                    throw new InvalidOperationException("Unknown calendar type.");
                }

                if (calendar.HasTag)
                    result.Append($" Tag: `{calendar.Tag}`");

                result.AppendLine();
            }                

            await command.Reply(result.ToString());
        }

        [Command("calendar", "split", "Splits a calendar in two by a given date.")]
        [Parameter("MessageId", ParameterType.Id, "message ID of the calendar; use `calendar list` to see all active calendars and their IDs")]
        [Parameter("Date", DateRegex, ParameterType.Regex, "a date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Comment("The calendar will be split in two. All events *before* the provided date will stay in the old calendar. All events from the provided date onwards (inclusive) will be displayed in a new calendar which will be created.")]
        [Example("462366629247057930 12/15")]
        public async Task SplitCalendar(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            // Get original calendar
            var settings = await _settings.Read<ScheduleSettings>(command.GuildId);
            var origCalendar = GetCalendarOfType<RangeScheduleCalendar>(settings, command["MessageId"].AsId.Value);

            // Check date
            if (!DateTime.TryParseExact(command["Date"], DateFormats, GlobalDefinitions.Culture, DateTimeStyles.None, out var date))
                throw new IncorrectParametersCommandException("Invalid date.", false);

            if (date <= origCalendar.BeginDate || date >= origCalendar.EndDate)
                throw new IncorrectParametersCommandException($"The date has to be betweeen the calendar's begin and end dates ({BuildCalendarSpanString(origCalendar)}).");

            // Create new calendar
            var newCalendar = new RangeScheduleCalendar()
            {
                Tag = origCalendar.Tag,
                ChannelId = origCalendar.ChannelId,
                BeginDate = date,
                EndDate = origCalendar.EndDate
            };

            try
            {
                var (text, embed) = BuildCalendarMessage(newCalendar, settings);
                var channel = await command.Guild.GetTextChannelAsync(origCalendar.ChannelId);
                if (channel == null)
                    throw new CommandException("Channel not found.");

                var messages = await _communicator.SendMessage(channel, text, embed);
                newCalendar.MessageId = messages.Single().Id;
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new CommandException($"Could not create the new calendar because it would be too long to display. Try narrowing the range of dates for the calendar.");
            }

            // Save
            settings = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                origCalendar = s.Calendars.FirstOrDefault(x => x.MessageId == command["MessageId"].AsId) as RangeScheduleCalendar;
                if (origCalendar == null)
                    throw new CommandException("Please try again."); // Race condition

                origCalendar.EndDate = date;
                s.Calendars.Add(newCalendar);
                return s;
            });

            var result = new RefreshResult(origCalendar, await RefreshCalendar(origCalendar, command.Guild, settings));
            await command.ReplySuccess($"The original calendar will now display events {BuildCalendarSpanString(origCalendar)}. A new calendar has been created to display events {BuildCalendarSpanString(newCalendar)}.\n**Tip:** You can reorder calendars in your schedule channel with the `calendar swap` or `calendar set begin/end` commands. {result.ToString(false)}");
        }

        [Command("calendar", "swap", "Swaps two calendars.")]
        [Alias("calendars", "swap")]
        [Parameter("FirstMessageId", ParameterType.Id, "message ID of the first calendar; use `calendar list` to see all active calendars and their IDs")]
        [Parameter("SecondMessageId", ParameterType.Id, "message ID of the second calendar")]
        [Comment("All properties and events of these two calendars will be swapped. Useful for reordering calendars in a #schedule channel.")]
        [Example("462366629247057930 524282594225815562")]
        public async Task SwapCalendars(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var (first, second, settings) = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var fc = GetCalendarOfType<ScheduleCalendar>(s, command["FirstMessageId"].AsId.Value);
                var sc = GetCalendarOfType<ScheduleCalendar>(s, command["SecondMessageId"].AsId.Value);

                var tmp = (fc.ChannelId, fc.MessageId);
                fc.ChannelId = sc.ChannelId;
                fc.MessageId = sc.MessageId;

                sc.ChannelId = tmp.ChannelId;
                sc.MessageId = tmp.MessageId;
                return (fc, sc, s);
            });

            var result = new RefreshResult();
            result.Add(first, await RefreshCalendar(first, command.Guild, settings));
            result.Add(second, await RefreshCalendar(second, command.Guild, settings));
            await command.ReplySuccess($"Calendars have been swapped. {result}");
        }

        [Command("calendar", "delete", "Deletes a calendar.")]
        [Parameter("MessageId", ParameterType.Id, "message ID of the calendar; use `calendar list` to display all active calendars and their message IDs")]
        [Comment("Deleting a calendar doesn't delete any events.")]
        public async Task DeleteCalendar(ICommand command, ILogger logger)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var removed = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var i = s.Calendars.FindIndex(x => x.MessageId == command["MessageId"].AsId);
                if (i < 0)
                    return null;

                var c = s.Calendars[i];
                s.Calendars.RemoveAt(i);
                return c;
            });

            if (removed == null)
                throw new IncorrectParametersCommandException("Cannot find a calendar with this message ID.");

            try
            {
                var channel = await command.Guild.GetTextChannelAsync(removed.ChannelId);
                if (channel != null)
                {
                    var message = (await channel.GetMessageAsync(removed.MessageId)) as IUserMessage;
                    if (message != null)
                        await message.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to remove calendar message {TargetMessageId}", removed.MessageId);
            }

            await command.ReplySuccess($"Calendar has been deleted.");
        }

        public static (string text, Embed embed) BuildCalendarMessage(ScheduleCalendar calendar, ScheduleSettings settings)
        {
            if (calendar is RangeScheduleCalendar rangeScheduleCalendar)
                return BuildCalendarMessage(rangeScheduleCalendar, settings);
            else if (calendar is UpcomingSpanScheduleCalendar upcomingSpanCalendar)
                return BuildCalendarMessage(upcomingSpanCalendar, settings);
            else if (calendar is UpcomingWeekScheduleCalendar upcomingWeekCalendar)
                return BuildCalendarMessage(upcomingWeekCalendar, settings);
            else
                throw new InvalidOperationException("Unknown calendar type.");
        }

        public static (string text, Embed embed) BuildCalendarMessage(RangeScheduleCalendar calendar, ScheduleSettings settings)
        {
            var result = new StringBuilder();
            var events = settings.Events
                .SkipWhile(x => x.Date < calendar.BeginDate)
                .TakeWhile(x => x.Date < calendar.EndDate)
                .Where(x => calendar.FitsTag(x.Tag));

            foreach (var e in events)
                result.AppendLine(FormatEvent(e, settings.EventFormat));

            if (result.Length > EmbedBuilder.MaxDescriptionLength)
                throw new ArgumentOutOfRangeException();

            string footer = calendar.Footer;
            if (string.IsNullOrEmpty(footer))
            {
                if (calendar.IsMonthCalendar && !string.IsNullOrEmpty(calendar.Title))
                    footer = $"{calendar.BeginDate.ToString("MMMM", GlobalDefinitions.Culture)} schedule • {BuildUTCOffsetString(settings)}";
                else
                    footer = $"All times in {BuildUTCOffsetString(settings)}";
            }

            var embed = new EmbedBuilder()
                .WithTitle(BuildCalendarTitle(calendar))
                .WithDescription(result.ToString())
                .WithFooter(footer);

            return (string.Empty, embed.Build());
        }

        public static (string text, Embed embed) BuildCalendarMessage(UpcomingSpanScheduleCalendar calendar, ScheduleSettings settings)
        {
            var (beginDate, endDate) = GetDateSpan(calendar, settings);
            var events = settings.Events
                .SkipWhile(x => x.Date < beginDate)
                .TakeWhile(x => x.Date < endDate)
                .Where(x => calendar.FitsTag(x.Tag))
                .ToList();

            var result = new StringBuilder();
            var displayed = 0;
            var today = DateTime.UtcNow.Add(settings.TimezoneOffset).Date;
            foreach (var e in events)
            {
                string line = FormatEvent(e, settings.EventFormat);
                if (result.Length + line.Length > EmbedBuilder.MaxDescriptionLength)
                    break;

                result.AppendLine(line + (e.Date.Date == today ? " `<today>`" : string.Empty));
                displayed++;
            }

            string footer = calendar.Footer;
            if (string.IsNullOrEmpty(footer))
            {
                footer = $"{BuildUTCOffsetString(settings)}";
                if (events.Count == displayed)
                    footer += calendar.DaysSpan > 0 ? $" • Shows the next {calendar.DaysSpan} days of events" : $" • All upcoming events";
                else
                    footer += $" • Shows next {displayed} events";
            }

            var embed = new EmbedBuilder()
                .WithTitle(BuildCalendarTitle(calendar))
                .WithDescription(result.ToString())
                .WithFooter(footer);

            return (string.Empty, embed.Build());
        }

        public static (string text, Embed embed) BuildCalendarMessage(UpcomingWeekScheduleCalendar calendar, ScheduleSettings settings)
        {
            var (beginDate, endDate) = GetDateSpan(calendar, settings);
            var events = settings.Events
                .SkipWhile(x => x.Date < beginDate)
                .TakeWhile(x => x.Date < endDate)
                .Where(x => calendar.FitsTag(x.Tag))
                .ToList();

            string footer = calendar.Footer;
            if (string.IsNullOrEmpty(footer))
                footer = $"All times in {BuildUTCOffsetString(settings)}";

            var embed = new EmbedBuilder().WithTitle(BuildCalendarTitle(calendar)).WithFooter(footer);
            var today = DateTime.UtcNow.Add(settings.TimezoneOffset).Date;
            var day = beginDate;
            while (day < endDate)
            {
                var nextDay = day.AddDays(1);
                var result = new StringBuilder();
                foreach (var e in events.SkipWhile(x => x.Date < day).TakeWhile(x => x.Date < nextDay))
                {
                    string line = FormatDayEvent(e, settings.EventFormat);
                    if (result.Length + line.Length > EmbedFieldBuilder.MaxFieldValueLength - 5)
                    {
                        result.AppendLine("...");
                        break;
                    }

                    result.AppendLine(line);
                }

                if (result.Length <= 0)
                    result.AppendLine("No events");

                string dayName;
                if (day == beginDate)
                    dayName = "Today";
                else if (day == beginDate.AddDays(1))
                    dayName = "Tomorrow";
                else
                    dayName = day.ToString("dddd", GlobalDefinitions.Culture);

                embed.AddField(x => x.WithIsInline(false).WithName(dayName).WithValue(result.ToString()));

                day = nextDay;
            }

            return (string.Empty, embed.Build());
        }

        public static string FormatEvent(ScheduleEvent e, EventFormat format, bool showId = false, bool showTag = false, bool showNotify = false)
        {
            var result = new StringBuilder();
            if (showId)
                result.Append($"`{e.Id:00}` ");

            if (format == EventFormat.KoreanDate)
                result.Append(e.Date.ToString(e.HasTime ? @"`[yyMMdd | HH:mm]` " : @"`[yyMMdd | ??:??]` ", GlobalDefinitions.Culture));
            else if (format == EventFormat.MonthName)
                result.Append(e.Date.ToString(e.HasTime ? @"`[MMM dd | HH:mm]` " : @"`[MMM dd | ??:??]` ", GlobalDefinitions.Culture));
            else
                result.Append(e.Date.ToString(e.HasTime ? @"`[MM\/dd | HH:mm]` " : @"`[MM\/dd | ??:??]` ", GlobalDefinitions.Culture));

            if (showTag && e.HasTag)
                result.Append($"`{e.Tag}` ");

            result.Append(e.HasLink ? DiscordHelpers.BuildMarkdownUri(e.Description, e.Link) : e.Description);

            if (showNotify && e.Notify)
                result.Append(" 🔔");

            return result.ToString();
        }

        public static string FormatDayEvent(ScheduleEvent e, EventFormat format)
        {
            var result = new StringBuilder();
            result.Append(e.HasTime ? e.Date.ToString(@"`HH:mm` ", GlobalDefinitions.Culture) : @"`??:??` ");
            result.Append(e.HasLink ? DiscordHelpers.BuildMarkdownUri(e.Description, e.Link) : e.Description);
            return result.ToString();
        }

        private async Task<(ScheduleEvent Event, string Message)> AddEventInner(ICommand command, bool notify = false)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            if (command["Tag"].HasValue && ReservedTags.Any(x => ScheduleSettings.CompareTag(command["Tag"], x, true)))
                throw new CommandException("This tag is reserved, please pick a different one.");

            ScheduleEvent e;
            try
            {
                // Parse datetime
                var dateTime = DateTime.ParseExact(command["Date"], DateFormats, GlobalDefinitions.Culture, DateTimeStyles.None);
                bool hasTime = command["Time"].HasValue && command["Time"].AsRegex.Groups[1].Success && command["Time"].AsRegex.Groups[2].Success;
                if (hasTime)
                    dateTime = dateTime.Add(new TimeSpan(int.Parse(command["Time"].AsRegex.Groups[1].Value), int.Parse(command["Time"].AsRegex.Groups[2].Value), 0));

                if (notify && !hasTime)
                    throw new CommandException("Notified events must have time specififed. Use `event add` to add events with an unknown time.");

                // Create event
                var link = DiscordHelpers.TryParseMarkdownUri(command["Description"]);
                e = new ScheduleEvent
                {
                    Tag = command["Tag"].HasValue ? command["Tag"].AsString : null,
                    Date = dateTime,
                    HasTime = hasTime,
                    Description = link.HasValue ? link.Value.Text : command["Description"],
                    Link = command["Link"].HasValue ? command["Link"].AsString : link?.Uri.AbsoluteUri,
                    Notify = notify
                };
            }
            catch (FormatException)
            {
                throw new IncorrectParametersCommandException("Invalid date.", false);
            }

            if (string.IsNullOrEmpty(e.Description))
                throw new IncorrectParametersCommandException("Description is required.");

            var settings = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                if (e.Notify && !s.Notifications.Any(x => e.FitsTag(x.Tag)))
                    throw new AbortException("Please first set up a channel which will receive the notifications for this tag with `schedule set notifications`.");

                e.Id = s.NextEventId++;
                s.Events.Add(e);
                return s;
            });

            if (e.Notify)
                await _service.RefreshNotifications(command.GuildId, settings);

            return (e, $"Event `{e.Description}` taking place on `{e.Date.ToString(@"yyyy\/MM\/dd", GlobalDefinitions.Culture)}`" + (e.HasTime ? $" at `{e.Date.ToString("HH:mm", GlobalDefinitions.Culture)}`" : string.Empty) + $" has been added with ID `{e.Id}`" + (e.HasTag ? $" and tag `{e.Tag}`" : string.Empty) + ".");
        }

        private async Task<(ScheduleEvent Event, string Message)> RemoveEventInner(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            if (command["IdOrSearchString"].AsInt.HasValue)
            {
                var (e, settings) = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
                {
                    var id = command["IdOrSearchString"].AsInt;
                    var i = s.Events.FindIndex(x => x.Id == id);
                    if (i < 0)
                        throw new CommandException($"Cannot find an event with ID `{id}`.");

                    var removed = s.Events[i];
                    s.Events.RemoveAt(i);
                    return (removed, s);
                });

                if (e == null)
                    throw new CommandException($"Can't find an event with ID `{command["IdOrSearchString"].AsInt}`.");

                if (e.Notify)
                    await _service.RefreshNotifications(command.GuildId, settings);

                return (e, $"Event `{e.Description}` has been removed.");
            }
            else
            {
                var searchString = command["IdOrSearchString"].AsString;
                var settings = await _settings.Read<ScheduleSettings>(command.GuildId);
                var events = settings.Events.Where(x => x.Description.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0);
                if (events.Skip(1).Any())
                {
                    // Multiple results
                    var pages = BuildEventList(settings, "Multiple matches", events, footer: "Please pick one event and run the command again with its ID number.");
                    throw new AbortException(pages);
                }
                else if (events.Count() == 1)
                {
                    var e = events.First();
                    await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
                    {
                        var i = s.Events.FindIndex(x => x.Id == e.Id);
                        if (i >= 0)
                        {
                            var removed = s.Events[i];
                            s.Events.RemoveAt(i);
                        }
                    });

                    if (e.Notify)
                        await _service.RefreshNotifications(command.GuildId, settings);

                    return (events.First(), $"Event `{e.Description}` with ID `{e.Id}` has been removed.");
                }
                else
                {
                    throw new AbortException($"No events found containing `{searchString}` in their description.");
                }
            }
        }

        private async Task<(DateTime OrigDate, ScheduleEvent Event, string Message)> EditEventInner(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            DateTime? date = null;
            TimeSpan? time = null;
            bool? hasTime = null;
            string description = null;
            var link = default(Optional<string>);
            bool? notify = null;
            try
            {
                if (command["Date"].HasValue)
                    date = DateTime.ParseExact(command["Date"], DateFormats, GlobalDefinitions.Culture, DateTimeStyles.None);

                if (command["Time"].HasValue)
                {
                    hasTime = command["Time"].HasValue && command["Time"].AsRegex.Groups[1].Success && command["Time"].AsRegex.Groups[2].Success;
                    if (hasTime.Value)
                        time = new TimeSpan(int.Parse(command["Time"].AsRegex.Groups[1].Value), int.Parse(command["Time"].AsRegex.Groups[2].Value), 0);
                }
            }
            catch (FormatException)
            {
                throw new IncorrectParametersCommandException("Invalid date.", false);
            }

            if (command["Description"].HasValue)
            {
                var markdownLink = DiscordHelpers.TryParseMarkdownUri(command["Description"]);
                description = markdownLink.HasValue ? markdownLink.Value.Text : command["Description"];

                if (markdownLink.HasValue)
                    link = markdownLink.Value.Uri.AbsoluteUri;
            }

            if (command["Link"].HasValue)
                link = (string)command["Link"];

            if (command["Nolink"].HasValue)
                link = null;

            if (description != null && string.IsNullOrWhiteSpace(description))
                throw new IncorrectParametersCommandException("Description cannot be empty.");

            if (command["Notification"].HasValue)
                notify = string.Compare(command["Notification"], "notify", true, GlobalDefinitions.Culture) == 0;

            var (settings, edited, originalDate, wasNotify) = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var i = s.Events.FindIndex(x => x.Id == (int)command["EventId"]);
                if (i < 0)
                    throw new CommandException("Cannot find an event with this ID.");

                var e = s.Events[i];
                s.Events.RemoveAt(i);

                var backup = (originalDate: e.Date, wasNotify: e.Notify);
                if (date.HasValue)
                    e.Date = date.Value.Date + e.Date.TimeOfDay;

                if (hasTime.HasValue)
                    e.HasTime = hasTime.Value;

                if (time.HasValue)
                    e.Date = e.Date.Date + time.Value;

                if (!string.IsNullOrWhiteSpace(description))
                    e.Description = description;

                if (link.IsSpecified)
                    e.Link = link.Value;

                if (notify.HasValue)
                    e.Notify = notify.Value;

                if (!e.HasTime && e.Notify)
                    throw new CommandException("Notified events must have time specififed.");

                if (e.Notify && !s.Notifications.Any(x => e.FitsTag(x.Tag)))
                    throw new AbortException("Please first set up a channel which will receive the notifications for this tag with `schedule set notifications`.");

                s.Events.Add(e); // Have to remove and re-add to sort properly
                return (s, e, backup.originalDate, backup.wasNotify);
            });

            if (edited.Notify || wasNotify)
                await _service.RefreshNotifications(command.GuildId, settings);

            return (originalDate, edited, $"Event `{edited.Id}` has been edited to `{edited.Description}` taking place on `{edited.Date.ToString(@"yyyy\/MM\/dd", GlobalDefinitions.Culture)}`" + (edited.HasTime ? $" at `{edited.Date.ToString("HH:mm", GlobalDefinitions.Culture)}`" : string.Empty) + $".");
        }

        private async Task<(string OriginalTag, ScheduleEvent Event, string Message)> TagEventInner(ICommand command)
        {
            await AssertPrivileges(command.Message.Author, command.GuildId);

            var tag = command["Tag"].AsString;
            var remove = ScheduleSettings.CompareTag(tag, NoneTag, true);
            if (!remove && ReservedTags.Any(x => ScheduleSettings.CompareTag(tag, x, true)))
                throw new CommandException("This tag is reserved, please pick a different one.");

            var (settings, edited, originalTag) = await _settings.Modify(command.GuildId, (ScheduleSettings s) =>
            {
                var i = s.Events.FindIndex(x => x.Id == (int)command["EventId"]);
                if (i < 0)
                    throw new CommandException("Cannot find an event with this ID.");

                var e = s.Events[i];
                s.Events.RemoveAt(i);

                var ot = e.Tag;
                e.Tag = remove ? null : tag;

                s.Events.Add(e); // Have to remove and re-add to sort properly
                return (s, e, ot);
            });

            return (originalTag, edited, $"Event `{edited.Id}` has been " + (remove ? "untagged." : $"tagged as `{edited.Tag}`."));
        }

        private Task<RefreshResult> RefreshCalendars(IGuild guild, DateTime? affectedTime = null, Optional<string> tag = default) => 
            RefreshCalendars(guild, affectedTime.HasValue ? new[] { affectedTime.Value } : Enumerable.Empty<DateTime>(), tag);

        private Task<RefreshResult> RefreshCalendars(IGuild guild, IEnumerable<DateTime> affectedTimes, Optional<string> tag = default) =>
            RefreshCalendars(guild, affectedTimes, tag.IsSpecified ? new[] { tag.Value } : Enumerable.Empty<string>());

        private async Task<RefreshResult> RefreshCalendars(IGuild guild, IEnumerable<DateTime> affectedTimes, IEnumerable<string> tags)
        {
            var settings = await _settings.Read<ScheduleSettings>(guild.Id);
            var result = new RefreshResult();
            tags = tags.ToList();
            foreach (var calendar in settings.Calendars)
            {
                var (beginDate, endDate) = GetDateSpan(calendar, settings);
                if (affectedTimes.Any() && affectedTimes.All(x => x < beginDate || x >= endDate))
                    continue;

                if (!tags.Any(x => calendar.FitsTag(x)))
                    continue;

                result.Add(calendar, await RefreshCalendar(calendar, guild, settings));
            }

            return result;
        }

        private async Task<RefreshResult.Reason> RefreshCalendar(ScheduleCalendar calendar, IGuild guild, ScheduleSettings settings)
        {
            var result = new RefreshResult();
            try
            {
                var channel = await guild.GetTextChannelAsync(calendar.ChannelId);
                var message = channel != null ? (await channel.GetMessageAsync(calendar.MessageId)) as IUserMessage : null;
                if (message == null)
                {
                    await _settings.Modify(guild.Id, (ScheduleSettings s) => s.Calendars.RemoveAll(x => x.MessageId == calendar.MessageId));
                    _logger.WithScope(guild).LogInformation("Removed deleted calendar {CalendarMessageId}", calendar.MessageId);
                    return RefreshResult.Reason.Removed;
                }

                var (text, embed) = BuildCalendarMessage(calendar, settings);
                await message.ModifyAsync(x => 
                { 
                    x.Content = text; 
                    x.Embed = embed; 
                });

                return RefreshResult.Reason.Success;
            }
            catch (ArgumentOutOfRangeException)
            {
                return RefreshResult.Reason.MessageTooLong;
            }
            catch (Exception ex)
            {
                _logger.WithScope(guild).LogError(ex, "Failed to update calendar {CalendarMessageId}", calendar.MessageId);
                return RefreshResult.Reason.Error;
            }
        }

        private async Task AssertPrivileges(IUser user, ulong guildId)
        {
            if (user is IGuildUser gu)
            {
                if (gu.GuildPermissions.ManageMessages)
                    return;

                var s = await _settings.Read<ScheduleSettings>(guildId);
                if (s.ScheduleRole == default || !gu.RoleIds.Contains(s.ScheduleRole))
                    throw new MissingPermissionsException("You may bypass this requirement by asking for a schedule manager role.", GuildPermission.ManageMessages);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static T GetCalendarOfType<T>(ScheduleSettings settings, ulong id)
            where T : ScheduleCalendar
        {
            var c = settings.Calendars.FirstOrDefault(x => x.MessageId == id);
            if (c == null)
                throw new IncorrectParametersCommandException($"Can't find a calendar with message ID `{id}`. Use `calendar list` to see all active calendars and their IDs.");

            var typed = c as T;
            if (typed == null)
            {
                if (typeof(T) == typeof(RangeScheduleCalendar))
                    throw new IncorrectParametersCommandException($"This command can only be used for standard calendars (created with `calendar create` or `calendar create month`).", false);
                else if (typeof(T) == typeof(UpcomingSpanScheduleCalendar))
                    throw new IncorrectParametersCommandException($"This command can only be used for upcoming event calendars (created with `calendar create upcoming`).", false);
                else if (typeof(T) == typeof(UpcomingWeekScheduleCalendar))
                    throw new IncorrectParametersCommandException($"This command can only be used for weekly calendars (created with `calendar create upcoming week`).", false);
                else
                    throw new IncorrectParametersCommandException($"This command cannot be used with this calendar type.", false);
            }
            else
            {
                return typed;
            }
        }

        private static (DateTime begin, DateTime end) GetDateSpan(ScheduleCalendar calendar, ScheduleSettings settings)
        {
            if (calendar is RangeScheduleCalendar rangeCalendar)
            {
                return (rangeCalendar.BeginDate, rangeCalendar.EndDate);
            }
            else if (calendar is UpcomingSpanScheduleCalendar upcomingSpanCalendar)
            {
                var beginDate = DateTime.UtcNow.Add(settings.TimezoneOffset).Date;
                var endDate = upcomingSpanCalendar.DaysSpan > 0 ? beginDate.AddDays(upcomingSpanCalendar.DaysSpan) : DateTime.MaxValue.Date;
                return (beginDate, endDate);
            }
            else if (calendar is UpcomingWeekScheduleCalendar upcomingWeekCalendar)
            {
                var beginDate = DateTime.UtcNow.Add(settings.TimezoneOffset).Date;
                var endDate = beginDate.AddDays(7);
                return (beginDate, endDate);
            }
            else
            {
                throw new InvalidOperationException("Unknown calendar type.");
            }
        }

        private static string BuildCalendarTitle(ScheduleCalendar calendar)
        {
            if (string.IsNullOrEmpty(calendar.Title))
            {
                if (calendar is RangeScheduleCalendar rangeCalendar && rangeCalendar.IsMonthCalendar)
                    return $"{rangeCalendar.BeginDate.ToString("MMMM", GlobalDefinitions.Culture)} schedule";
                else if (calendar is UpcomingSpanScheduleCalendar upcomingSpanCalendar)
                    return "Upcoming events";
                else if (calendar is UpcomingWeekScheduleCalendar upcomingWeekCalendar)
                    return "This week's schedule";
                else
                    return "Schedule";
            }
            else
            {
                return calendar.Title;
            }
        }

        private static PageCollection BuildEventList(ScheduleSettings settings, string header, DateTime? beginDate = null, DateTime? endDate = null, Func<ScheduleEvent, bool> predicate = null, string footer = null)
        {
            if (predicate == null)
                predicate = x => true;

            if (beginDate == null)
                beginDate = DateTime.MinValue;

            if (endDate == null)
                endDate = DateTime.MaxValue;

            return BuildEventList(settings, header, settings.Events.SkipWhile(x => x.Date < beginDate).TakeWhile(x => x.Date < endDate).Where(predicate), footer);
        }

        private static PageCollection BuildEventList(ScheduleSettings settings, string header, IEnumerable<ScheduleEvent> events, string footer = null)
        {
            var result = new StringBuilder();
            var pages = new PageCollection();
            var embedBuilder = new Func<string, EmbedBuilder>((d) => new EmbedBuilder()
                .WithTitle(header)
                .WithDescription(d)
                .WithFooter(footer ?? BuildUTCOffsetString(settings)));

            foreach (var e in events)
            {
                var line = FormatEvent(e, settings.EventFormat, showId: true, showTag: true, showNotify: true);
                if (result.Length + line.Length + Environment.NewLine.Length > EmbedBuilder.MaxDescriptionLength)
                {
                    pages.Add(embedBuilder(result.ToString()));
                    result.Clear();
                }

                result.AppendLine(line);
            }

            if (result.Length > 0)
                pages.Add(embedBuilder(result.ToString()));

            return pages;
        }

        private static string BuildUTCOffsetString(ScheduleSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.TimezoneName))
                return settings.TimezoneName;

            var result = new StringBuilder("UTC");
            if (settings.TimezoneOffset != TimeSpan.Zero)
            {
                result.Append($"{Math.Truncate(settings.TimezoneOffset.TotalHours):+#0;-#0}");
                if (settings.TimezoneOffset.Minutes != 0)
                    result.Append($":{Math.Abs(settings.TimezoneOffset.Minutes):00}");
            }

            return result.ToString();
        }

        private static string BuildCalendarSpanString(RangeScheduleCalendar calendar)
            => $"from `{calendar.BeginDate.ToString(@"yyyy\/MM\/dd", GlobalDefinitions.Culture)}`{(calendar.HasEndDate ? $" to `{calendar.EndDate.AddDays(-1).ToString(@"yyyy\/MM\/dd", GlobalDefinitions.Culture)}`" : string.Empty)} (inclusive)";

        private string BuildCalendarTagString(ScheduleCalendar calendar, string prefix = "")
        {
            if (!string.IsNullOrEmpty(prefix))
                prefix += " ";

            if (calendar.HasAllTag)
                return $" {prefix}with any tag";
            else if (calendar.HasTag)
                return $" {prefix}marked with tag `{calendar.Tag}`";
            else
                return string.Empty;
        }

        private static string BuildTagString(string tag)
        {
            if (ScheduleSettings.IsAllTag(tag))
                return $"with any tag";
            else if (!ScheduleSettings.IsDefaultTag(tag))
                return $"marked with tag `{tag}`";
            else
                return "without tags";
        }
    }
}
