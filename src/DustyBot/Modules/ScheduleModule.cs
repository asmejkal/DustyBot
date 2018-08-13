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
    [Module("Schedule", "Helps with tracking upcoming events.")]
    class ScheduleModule : Module
    {
        const string DateFormat = @"^(?:([0-9]{4})\/)?([0-9]{1,2})\/([0-9]{1,2})$";
        const string TimeFormat = @"^([0-9]{1,2}):([0-9]{1,2})|\?\?:\?\?$";

        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public DiscordSocketClient Client { get; private set; }

        public ScheduleModule(ICommunicator communicator, ISettingsProvider settings, DiscordSocketClient client)
        {
            Communicator = communicator;
            Settings = settings;
            Client = client;
        }
        
        [Command("schedule", "Shows upcoming events.")]
        public async Task Schedule(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId, false);
            if (settings == null || settings.ScheduleMessages.Count <= 0)
            {
                await command.ReplyError(Communicator, "No schedule has been set. Use the `schedule create` command.").ConfigureAwait(false);
                return;
            }

            var events = new List<ScheduleEvent>();
            var channelReferences = new HashSet<string>();
            var currentTime = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"));
            foreach (var messageLoc in settings.ScheduleMessages)
            {
                var channel = await command.Guild.GetTextChannelAsync(messageLoc.ChannelId).ConfigureAwait(false);
                var message = await channel?.GetMessageAsync(messageLoc.MessageId) as IUserMessage;
                if (message == null)
                {
                    //Deleted
                    await Settings.Modify(command.GuildId, (MediaSettings x) => x.ScheduleMessages.Remove(messageLoc));
                    continue;
                }

                var schedule = ScheduleMessageFactory.TryParse(message);
                if (schedule == null)
                    continue;

                events.AddRange(schedule.GetEvents(currentTime.AddHours(-2), currentTime.AddDays(14)));
                channelReferences.Add("#" + channel.Name);
            }

            if (events.Count <= 0)
            {
                await command.Reply(Communicator, "No events planned for the next two weeks.").ConfigureAwait(false);
                return;
            }

            string result = string.Empty;
            foreach (var item in events.OrderBy(x => x.Date))
            {
                result += "\n" + item.Date.ToString(item.HasTime ? @"`[MM\/dd | HH:mm]`" : @"`[MM\/dd | ??:??]`") + " " + item.Description;

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
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("Channel", ParameterType.TextChannel, "target channel")]
        [Parameter("Header", ParameterType.String, ParameterFlags.Optional, "header for the message")]
        [Parameter("Footer", ParameterType.String, ParameterFlags.Optional, "footer for the message")]
        [Comment("Sends an empty schedule message (e.g. to your `#schedule` channel). You can then add events with the `event add` command.")]
        public async Task CreateSchedule(ICommand command)
        {
            var message = await DefaultScheduleMessage.Create(command[0].AsTextChannel, command[1], command[2]);

            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                s.ScheduleMessages.Add(new MessageLocation() { MessageId = message.Message.Id, ChannelId = command[0].AsTextChannel.Id });
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, $"Schedule message with ID `{message.Message.Id}` has been created. Use the `event add` or `event add {message.Message.Id}` command to add events.").ConfigureAwait(false);
        }

        [Command("schedule", "edit", "header", "Sets a header for a schedule message.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.Id, "ID of a schedule message previously created with `schedule create`")]
        [Parameter("Header", ParameterType.String, ParameterFlags.Remainder, "new header")]
        [Example("462366629247057930 Schedule (June)")]
        public async Task HeaderSchedule(ICommand command)
        {
            var message = await GetScheduleMessage(command.Guild, (ulong?)command[0]);

            message.Header = command.Remainder.After(1);
            await message.CommitChanges();

            await command.ReplySuccess(Communicator, $"Header has been set.").ConfigureAwait(false);
        }

        [Command("schedule", "edit", "footer", "Sets a footer for a schedule message.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.Id, "ID of a schedule message previously created with `schedule create`")]
        [Parameter("Footer", ParameterType.String, ParameterFlags.Remainder, "new footer")]
        [Example("462366629247057930 Ooh, a new footer.")]
        public async Task FooterSchedule(ICommand command)
        {
            var message = await GetScheduleMessage(command.Guild, (ulong?)command[0]);

            message.Footer = command.Remainder.After(1);
            await message.CommitChanges();

            await command.ReplySuccess(Communicator, $"Footer has been set.").ConfigureAwait(false);
        }

        private static readonly Regex _editEventRegex = new Regex(@"^\s*\[([0-9]{4})\/([0-9]+)\/([0-9]+)\s*\|\s*([0-9?]+):([0-9?]+)\]\s*(.*)$");
        [Command("schedule", "edit", "Edits the content of a schedule message.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.Id, "ID of a schedule message previously created with `schedule create`")]
        [Parameter("Events", @"^\s*\[[0-9]{4}\/[0-9]+\/[0-9]+\s*\|\s*[0-9?]+:[0-9?]+\].+", ParameterType.String, ParameterFlags.Remainder, "new content")]
        [Comment("Old content will be replaced. The new content has to be in the following format:\n[2018/08/10 | 08:00] Event 1\n[2018/08/11 | ??:??] Event 2\n[2018/08/12 | 10:00] Event 3\n...\n\nEach event has to be a on a new line.")]
        [Example("462366629247057930\n[2018/08/10 | 08:00] Event 1\n[2018/08/11 | ??:??] Event 2\n[2018/08/12 | 10:00] Event 3")]
        public async Task EditSchedule(ICommand command)
        {
            var message = await GetScheduleMessage(command.Guild, (ulong?)command[0]);

            message.Clear();
            var content = command.Remainder.After(command.GetIndex("Events"));
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
                            Description = match.Groups[6].Value.Trim(),
                            HasTime = !match.Groups[4].Value.Contains('?') && !match.Groups[5].Value.Contains('?')
                        };

                        newEvent.Date = new DateTime(int.Parse(match.Groups[1].Value),
                            int.Parse(match.Groups[2].Value),
                            int.Parse(match.Groups[3].Value),
                            newEvent.HasTime ? int.Parse(match.Groups[4].Value) : 23,
                            newEvent.HasTime ? int.Parse(match.Groups[5].Value) : 59, 0);

                        message.Add(newEvent);
                    }
                    catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                    {
                        throw new Framework.Exceptions.IncorrectParametersCommandException($"Line `{line}` is invalid");
                    }
                }
            }

            await message.CommitChanges();

            await command.ReplySuccess(Communicator, $"Message has been edited.").ConfigureAwait(false);
        }

        [Command("schedule", "move", "Moves a range of events from one message to another.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("SourceMessageId", ParameterType.Id, "ID of the source message (previously created with `schedule create`)")]
        [Parameter("TargetMessageId", ParameterType.Id, "ID of the target message (previously created with `schedule create`)")]
        [Parameter("FromDate", DateFormat, ParameterType.Regex, "move events from this date onward; date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Parameter("ToDate", DateFormat, ParameterType.Regex, ParameterFlags.Optional, "move events before this date")]
        [Example("462366629247057930 476740163159195649 08/09")]
        [Example("462366629247057930 476740163159195649 2018/08/09 2018/12/01")]
        public async Task SplitSchedule(ICommand command)
        {
            var source = await GetScheduleMessage(command.Guild, (ulong?)command[0]);
            var target = await GetScheduleMessage(command.Guild, (ulong?)command[1]);
            
            var fromDate = DateTime.ParseExact(command["FromDate"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None);
            var toDate = command["ToDate"].HasValue ? DateTime.ParseExact(command["ToDate"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None) + new TimeSpan(23, 59, 0) : DateTime.MaxValue;

            var moved = source.MoveAll(target, x => x.Date >= fromDate && x.Date < toDate);

            if (moved > 0)
            {
                await target.CommitChanges();
                await source.CommitChanges();
            }

            await command.ReplySuccess(Communicator, $"Moved {moved} events.").ConfigureAwait(false);
        }

        [Command("schedule", "ignore", "Stops using a schedule message.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.Id)]
        [Comment("Stops using this message for the `schedule` and `event add/remove` commands. Do this for old schedule messages to improve response times (if you don't want to delete them).")]
        public async Task IgnoreSchedule(ICommand command)
        {
            bool removed = await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                return s.ScheduleMessages.RemoveAll(x => x.MessageId == (ulong)command[0]) > 0;
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
        [Permissions(GuildPermission.ManageMessages)]
        public async Task ListSchedule(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId, false).ConfigureAwait(false);
            if (settings == null || settings.ScheduleMessages.Count <= 0)
            {
                await command.Reply(Communicator, $"No schedule sources.").ConfigureAwait(false);
                return;
            }

            var result = "";
            foreach (var message in settings.ScheduleMessages)
                result += $"Channel: `{message.ChannelId}` Id: `{message.MessageId}`\n";

            await command.Reply(Communicator, result).ConfigureAwait(false);
        }

        [Command("event", "add", "Adds an event to schedule.")]
        [Permissions(GuildPermission.ManageMessages)]
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
            var schedule = await GetScheduleMessage(command.Guild, (ulong?)command[0]);

            var dateTime = DateTime.ParseExact(command["Date"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None);
            bool hasTime = command["Time"].HasValue && command["Time"].AsRegex.Groups[1].Success && command["Time"].AsRegex.Groups[2].Success;
            if (hasTime)
                dateTime = dateTime.Add(new TimeSpan(int.Parse(command["Time"].AsRegex.Groups[1].Value), int.Parse(command["Time"].AsRegex.Groups[2].Value), 0));

            var e = new ScheduleEvent
            {
                Date = dateTime,
                HasTime = hasTime,
                Description = command.Remainder.After(command.GetIndex("Description"))
            };

            if (string.IsNullOrEmpty(e.Description))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Description is required.");

            schedule.Add(e);
            try
            {
                await schedule.CommitChanges();
            }
            catch (ArgumentOutOfRangeException)
            {
                await command.ReplyError(Communicator, "The message is too long. Create a new one with the `schedule create` command.").ConfigureAwait(false);
                return;
            }

            await command.ReplySuccess(Communicator, $"Event `{e.Description}` taking place on `{e.Date.ToString(@"yy\/MM\/dd")}`" + (e.HasTime ? $" at `{e.Date.ToString("HH:mm")}`" : "") + " has been added.").ConfigureAwait(false);
        }

        [Command("event", "remove", "Removes an event from schedule.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.Id, ParameterFlags.Optional, "ID of a schedule message previously created with `schedule create`, uses the latest by default")]
        [Parameter("Date", DateFormat, ParameterType.Regex, ParameterFlags.Optional, "date in `MM/dd` or `yyyy/MM/dd` format (e.g. `07/23` or `2018/07/23`), uses current year by default")]
        [Parameter("Time", TimeFormat, ParameterType.Regex, ParameterFlags.Optional, "time in `HH:mm` format (eg. `08:45`)")]
        [Parameter("Description", ParameterType.String, ParameterFlags.Remainder, "event description")]
        [Comment("All times in KST.")]
        [Example("Concert")]
        [Example("462366629247057930 07/23 08:45 Festival")]
        public async Task RemoveEvent(ICommand command)
        {
            var schedule = await GetScheduleMessage(command.Guild, (ulong?)command[0]);

            string description = command.Remainder.After(command.GetIndex("Description"));

            if (string.IsNullOrEmpty(description))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Description is required.");

            DateTime? date = command["Date"].HasValue ? new DateTime?(DateTime.ParseExact(command["Date"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None)) : null;
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

        [Command("schedule", "update", "Updates all messages.", CommandFlags.OwnerOnly)]
        public async Task UpdateSchedule(ICommand command)
        {
            foreach (var settings in await Settings.Read<MediaSettings>())
            {
                var guild = Client.GetGuild(settings.ServerId);
                if (guild == null)
                    continue;

                foreach (var messageLoc in settings.ScheduleMessages)
                {
                    var channel = guild.TextChannels.FirstOrDefault(x => x.Id == messageLoc.ChannelId);
                    if (channel == null)
                    {
                        await Settings.Modify(settings.ServerId, (MediaSettings x) => x.ScheduleMessages.RemoveAll(y => y.ChannelId == messageLoc.ChannelId));
                        continue;
                    }

                    var message = await channel.GetMessageAsync(messageLoc.MessageId);
                    if (message == null)
                    {
                        await Settings.Modify(settings.ServerId, (MediaSettings x) => x.ScheduleMessages.RemoveAll(y => y.MessageId == messageLoc.MessageId));
                        continue;
                    }

                    if (message.Embeds.Count > 0)
                        continue;

                    var schedule = ScheduleMessageFactory.TryParse(message as IUserMessage);
                    await schedule.CommitChanges();
                }
                
            }
        }

        async Task<IScheduleMessage> GetScheduleMessage(IGuild guild, ulong? messageId = null, bool mustBeEditable = true)
        {
            var settings = await Settings.Read<MediaSettings>(guild.Id);
            MessageLocation messageLoc = messageId.HasValue ?
                settings.ScheduleMessages.FirstOrDefault(x => x.MessageId == messageId) :
                settings.ScheduleMessages.LastOrDefault();

            if (messageLoc == null)
                throw new Framework.Exceptions.IncorrectParametersCommandException(messageId.HasValue ? "This message hasn't been created by `schedule create`." : "No schedule message has been created. Use `schedule create`.");

            var channel = await guild.GetTextChannelAsync(messageLoc.ChannelId);
            var message = await channel?.GetMessageAsync(messageLoc.MessageId) as IUserMessage;
            if (message == null)
            {
                //Deleted
                await Settings.Modify(guild.Id, (MediaSettings x) => x.ScheduleMessages.Remove(messageLoc));
                throw new Framework.Exceptions.IncorrectParametersCommandException($"Cannot find the schedule message (Id: `{messageLoc.MessageId}`).");
            }

            var schedule = ScheduleMessageFactory.TryParse(message);
            if (schedule == null)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid schedule format.");

            if (mustBeEditable && !schedule.IsEditable())
                throw new Framework.Exceptions.IncorrectParametersCommandException("The bot cannot edit this message. You can only edit messages sent by the `schedule create` command.");

            return schedule;
        }

        struct ScheduleEvent
        {
            public DateTime Date { get; set; }
            public bool HasTime { get; set; }
            public string Description { get; set; }
        }

        interface IScheduleMessage
        {
            IReadOnlyList<ScheduleEvent> Events { get; }

            string Header { get; set; }
            string Footer { get; set; }

            IEnumerable<ScheduleEvent> GetEvents(DateTime from, DateTime to);
            void Add(ScheduleEvent e);
            void Remove(ScheduleEvent e);
            int RemoveAll(Predicate<ScheduleEvent> predicate);
            int MoveAll(IScheduleMessage other, Predicate<ScheduleEvent> predicate);
            void Clear();

            bool IsEditable();
            Task CommitChanges();

            IUserMessage Message { get; }
        }

        static class ScheduleMessageFactory
        {
            public static IScheduleMessage TryParse(IUserMessage message)
            {
                IScheduleMessage result = null;
                if (result == null)
                    result = DefaultScheduleMessage.TryParse(message);

                return result;
            }
        }

        class DefaultScheduleMessage : IScheduleMessage
        {
            public const string DefaultHeader = "Schedule";
            private static Regex _scheduleLineRegex = new Regex(@"\s*`\[([0-9]{4})\/([0-9]+)\/([0-9]+)\s*\|\s*([0-9?]+):([0-9?]+)\]`\s*(.*)", RegexOptions.Compiled);

            private List<ScheduleEvent> _events = new List<ScheduleEvent>();
            private IUserMessage _message;

            public string Header { get; set; }
            public string Footer { get; set; }
            public IReadOnlyList<ScheduleEvent> Events => _events as IReadOnlyList<ScheduleEvent>;
            public IUserMessage Message => _message;

            private DefaultScheduleMessage(IUserMessage message, string header, string footer)
            {
                _message = message;
                Header = header;
                Footer = footer;
            }

            public static IScheduleMessage TryParseLegacy(IUserMessage message)
            {
                var begin = message.Content.IndexOf("```");
                var end = message.Content.LastIndexOf("```");
                if (begin < 0 || end < 0)
                    return null;

                var result = new DefaultScheduleMessage(message, message.Content.Substring(0, begin), message.Content.Substring(end + 3));

                begin += 3;
                if (begin >= end)
                    return null;

                var schedule = message.Content.Substring(begin, end - begin);
                using (var reader = new StringReader(schedule))
                {
                    for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        try
                        {
                            var match = Regex.Match(line, @"\s*\[([0-9]+)\/([0-9]+)\s*\|\s*([0-9?]+):([0-9?]+)\]\s*(.*)", RegexOptions.Compiled);

                            if (match.Groups.Count < 6)
                                continue;

                            var newEvent = new ScheduleEvent
                            {
                                Description = match.Groups[5].Value.Trim(),
                                HasTime = !match.Groups[3].Value.Contains('?') && !match.Groups[4].Value.Contains('?')
                            };

                            newEvent.Date = new DateTime(DateTime.Now.Year,
                                int.Parse(match.Groups[1].Value),
                                int.Parse(match.Groups[2].Value),
                                newEvent.HasTime ? int.Parse(match.Groups[3].Value) : 23,
                                newEvent.HasTime ? int.Parse(match.Groups[4].Value) : 59, 0);

                            result._events.Add(newEvent);
                        }
                        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                        {
                            continue;
                        }
                    }
                }

                result._events = result._events.OrderBy(x => x.Date).ToList();
                return result;
            }

            public static IScheduleMessage TryParse(IUserMessage message)
            {
                var embed = message.Embeds.FirstOrDefault();
                if (embed == null)
                    return TryParseLegacy(message);
                
                var result = new DefaultScheduleMessage(message, embed.Title, embed.Footer?.Text);

                var schedule = embed.Description ?? string.Empty;
                using (var reader = new StringReader(schedule))
                {
                    for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        try
                        {
                            var match = _scheduleLineRegex.Match(line);

                            if (!match.Success || match.Groups.Count < 7)
                                continue;

                            var newEvent = new ScheduleEvent
                            {
                                Description = match.Groups[6].Value.Trim(),
                                HasTime = !match.Groups[4].Value.Contains('?') && !match.Groups[5].Value.Contains('?')
                            };

                            newEvent.Date = new DateTime(int.Parse(match.Groups[1].Value),
                                int.Parse(match.Groups[2].Value),
                                int.Parse(match.Groups[3].Value),
                                newEvent.HasTime ? int.Parse(match.Groups[4].Value) : 23,
                                newEvent.HasTime ? int.Parse(match.Groups[5].Value) : 59, 0);

                            result._events.Add(newEvent);
                        }
                        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                        {
                            continue;
                        }
                    }
                }

                result._events = result._events.OrderBy(x => x.Date).ToList();
                return result;
            }

            public static async Task<IScheduleMessage> Create(ITextChannel channel, string header, string footer)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(string.IsNullOrEmpty(header) ? DefaultHeader : header)
                    .WithDescription("")
                    .WithFooter(footer);

                var message = await channel.SendMessageAsync(string.Empty, embed: embed.Build());
                return new DefaultScheduleMessage(message, header, footer);
            }

            public void Add(ScheduleEvent e)
            {
                if (!e.HasTime)
                    e.Date = new DateTime(e.Date.Year, e.Date.Month, e.Date.Day, 23, 59, 0, 0); //Standardize and sort at the end

                var where = _events.FindIndex(x => x.Date > e.Date);
                _events.Insert(where < 0 ? _events.Count : where, e);
            }

            public async Task CommitChanges()
            {
                var result = new StringBuilder();
                foreach (var e in Events)
                    result.AppendLine(e.Date.ToString(e.HasTime ? @"`[yyyy\/MM\/dd | HH:mm]`" : @"`[yyyy\/MM\/dd | ??:??]`") + " " + e.Description);

                if (result.Length > 2000)
                    throw new ArgumentException();

                var embed = new EmbedBuilder()
                    .WithTitle(string.IsNullOrEmpty(Header) ? DefaultHeader : Header)
                    .WithDescription(result.ToString())
                    .WithFooter(Footer);

                await _message.ModifyAsync(x => { x.Content = string.Empty; x.Embed = embed.Build(); });
            }

            public IEnumerable<ScheduleEvent> GetEvents(DateTime from, DateTime to) => Events.SkipWhile(x => x.Date < from).TakeWhile(x => x.Date < to);
            public bool IsEditable() => _message.Author.Id == ((_message.Channel as ITextChannel)?.Guild as SocketGuild)?.CurrentUser.Id;
            public void Remove(ScheduleEvent e) => _events.Remove(e);
            public int RemoveAll(Predicate<ScheduleEvent> predicate) => _events.RemoveAll(predicate);

            public int MoveAll(IScheduleMessage other, Predicate<ScheduleEvent> predicate)
            {
                int count = 0;
                for (int i = _events.Count - 1; i >= 0; i--)
                {
                    if (!predicate(_events[i]))
                        continue;

                    other.Add(_events[i]);
                    _events.RemoveAt(i);
                    count++;
                }

                return count;
            }

            public void Clear() => _events.Clear();
        }
    }
}
