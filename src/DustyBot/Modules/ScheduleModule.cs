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
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }

        public ScheduleModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }
        
        [Command("schedule", "Shows the upcoming schedule.")]
        public async Task Schedule(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId, false);
            if (settings == null || settings.ScheduleMessages.Count <= 0)
            {
                await command.ReplyError(Communicator, "No schedule has been set. Use the `schedule add` command.").ConfigureAwait(false);
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
        [Parameters(ParameterType.TextChannel)]
        [Permissions(GuildPermission.ManageMessages)]
        [Usage("{p}schedule create `Channel` `[Header]` `[Footer]`\n\n● `Channel` - target channel\n● `Header` - optional; header for the message\n● `Footer` - optional; footer for the message\n\nSends an empty schedule message (e.g. to your `#schedule` channel). You can then add events with the `event add` command.")]
        public async Task CreateSchedule(ICommand command)
        {
            var message = await DefaultScheduleMessage.Create(command[0].AsTextChannel, command[1], command[2]);

            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                s.ScheduleMessages.Add(new MessageLocation() { MessageId = message.Message.Id, ChannelId = command[0].AsTextChannel.Id });
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, $"Schedule message with ID `{message.Message.Id}` has been created. Use the `event add {message.Message.Id}` command to add events.").ConfigureAwait(false);
        }

        [Command("schedule", "add", "Adds an existing message with schedule.")]
        [Parameters(ParameterType.ULong)]
        [Permissions(GuildPermission.ManageMessages)]
        [Usage("{p}schedule add `MessageId`\n\nThe `schedule` command uses data from messages containing a schedule, which are already posted on your server. Expected format of a message with schedule is the following:\n\n[optional text...]```[MM/DD | HH:MM] Event description\n[MM/DD | HH:MM] Another event's description```[optional text...]\n\nThe `HH:MM` can be replaced with `??:??` if the event time is unknown. All times in KST.\n\nIf your server uses a different format for schedule posts and you aren't willing to change it, you can make a suggestion to have your format added with the `feedback` command, or by contacting the bot owner.\n\n__Example:__ {p}schedule add 464071206920781835")]
        public async Task AddSchedule(ICommand command)
        {
            var messageLoc = await command.Guild.GetMessageAsync((ulong)command[0]);
            if (messageLoc == null)
            {
                await command.ReplyError(Communicator, "Couldn't find the specified message.").ConfigureAwait(false);
                return;
            }

            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                s.ScheduleMessages.Add(new MessageLocation() { MessageId = messageLoc.Item1.Id, ChannelId = messageLoc.Item2.Id });
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, "Schedule message has been added.").ConfigureAwait(false);
        }

        [Command("schedule", "remove", "Removes a schedule message.")]
        [Parameters(ParameterType.ULong)]
        [Permissions(GuildPermission.ManageMessages)]
        [Usage("{p}schedule remove `MessageId`\n\nDoesn't delete the message, just stops using it for schedule.")]
        public async Task RemoveSchedule(ICommand command)
        {
            bool removed = await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                return s.ScheduleMessages.RemoveWhere(x => x.MessageId == (ulong)command.GetParameter(0)) > 0;
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
        [Usage("{p}schedule list")]
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

        [Command("schedule", "clear", "Removes all schedule messages.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Usage("{p}schedule clear\n\nDoesn't delete the messages, just stops using them for schedule.")]
        public async Task ClearSchedule(ICommand command)
        {
            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                s.ScheduleMessages.Clear();
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, $"Schedule has been cleared.").ConfigureAwait(false);
        }

        static Regex DateRegex = new Regex(@"^([0-9]{1,2})\/([0-9]{1,2})$", RegexOptions.Compiled);
        static Regex TimeRegex = new Regex(@"^([0-9]{1,2}):([0-9]{1,2})$", RegexOptions.Compiled);

        [Command("event", "add", "Adds an event to schedule.")]
        [Parameters(ParameterType.ULong, ParameterType.String, ParameterType.String)]
        [Permissions(GuildPermission.ManageMessages)]
        [Usage("{p}event add `MessageId` `[Year]` `Date` `[Time]` `Description...`\n\n● `MessageId` - ID of a schedule message previously created with `schedule create`\n● `Year` - optional; year of the event, uses current year by default\n● `Date` - date in `MM/dd` format (e.g. `07/23`)\n● `Time` - optional; time in `HH:mm` format (eg. `08:45`); skip if the time is unknown\n● `Description` - remainder; event description\n\nAll times in KST.\n\n__Examples:__\n{p}event add 462366629247057930 07/23 08:45 Concert\n{p}event add 462366629247057930 07/23 Fansign")]
        public async Task AddEvent(ICommand command)
        {
            var message = (await command.Guild.GetMessageAsync((ulong)command[0]))?.Item1 as IUserMessage;
            if (message == null)
            {
                await command.ReplyError(Communicator, $"Cannot find this message.").ConfigureAwait(false);
                return;
            }

            var schedule = ScheduleMessageFactory.TryParse(message);
            if (schedule == null)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid schedule format.");

            if (!schedule.IsEditable())
            {
                await command.ReplyError(Communicator, "The bot cannot edit this message. You can only edit messages sent by the `schedule create` command.").ConfigureAwait(false);
                return;
            }

            int year = -1;
            if (command[1].AsString.All(x => char.IsDigit(x)))
                year = (int)command[0];

            bool hasYear = year >= 0;
            
            var date = command[!hasYear ? 1 : 2].AsString != null ? DateRegex.Match(command[!hasYear ? 1 : 2]) : Match.Empty;
            var time = command[!hasYear ? 2 : 3].AsString != null ? TimeRegex.Match(command[!hasYear ? 2 : 3]) : Match.Empty;
            if (!date.Success)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid date.");

            if (!hasYear)
                 year = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time")).Year;

            DateTime dateTime = new DateTime(year, int.Parse(date.Groups[1].Value), int.Parse(date.Groups[2].Value));
            if (time.Success)
                dateTime = dateTime.Add(new TimeSpan(int.Parse(time.Groups[1].Value), int.Parse(time.Groups[2].Value), 0));

            var e = new ScheduleEvent
            {
                Date = dateTime,
                HasTime = time.Success,
                Description = command.Remainder.After(2 + (hasYear ? 1 : 0) + (time.Success ? 1 : 0))
            };

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

            await command.ReplySuccess(Communicator, $"Event `{e.Description}` taking place on `{e.Date.Year}/{e.Date.Month}/{e.Date.Day}`" + (e.HasTime ? $" at `{e.Date.Hour}:{e.Date.Minute}`" : "") + " has been added.").ConfigureAwait(false);
        }

        [Command("event", "remove", "Removes an event from schedule.")]
        [Parameters(ParameterType.ULong, ParameterType.String)]
        [Permissions(GuildPermission.ManageMessages)]
        [Usage("{p}event remove `MessageId` `[Date]` `[Time]` `Description...`\n\n● `MessageId` - ID of a schedule message previously created with `schedule create`\n● `Date` - optional; date in `MM/dd` format (e.g. `07/23`)\n● `Time` - optional; time in `HH:mm` format (eg. `08:45`)\n● `Description` - remainder; event description\n\nAll times in KST.\n\n__Examples:__\n{p}event remove 462366629247057930 Concert\n{p}event remove 462366629247057930 07/23 Fansign\n{p}event remove 462366629247057930 07/23 08:45 Festival")]
        public async Task RemoveEvent(ICommand command)
        {
            var message = (await command.Guild.GetMessageAsync((ulong)command[0]))?.Item1 as IUserMessage;
            if (message == null)
            {
                await command.ReplyError(Communicator, $"Cannot find this message.").ConfigureAwait(false);
                return;
            }

            var schedule = ScheduleMessageFactory.TryParse(message);
            if (schedule == null)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid schedule format.");

            if (!schedule.IsEditable())
            {
                await command.ReplyError(Communicator, "The bot cannot edit this message. You can only edit messages sent by the `schedule create` command.").ConfigureAwait(false);
                return;
            }
            
            var date = command[1].AsString != null ? DateRegex.Match(command[1]) : Match.Empty;
            var time = command[2].AsString != null ? DateRegex.Match(command[2]) : Match.Empty;
            string description = command.Remainder.After(1 + (date.Success ? 1 : 0) + (time.Success ? 1 : 0));

            var removed = schedule.RemoveAll(x =>
            {
                if (string.Compare(x.Description, description, true) != 0)
                    return false;

                if (date.Success && (int.Parse(date.Groups[1].Value) != x.Date.Month || int.Parse(date.Groups[2].Value) != x.Date.Day))
                    return false;

                if (time.Success && (int.Parse(time.Groups[1].Value) != x.Date.Hour || int.Parse(time.Groups[2].Value) != x.Date.Minute))
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

        struct ScheduleEvent
        {
            public DateTime Date { get; set; }
            public bool HasTime { get; set; }
            public string Description { get; set; }
        }

        interface IScheduleMessage
        {
            IReadOnlyList<ScheduleEvent> Events { get; }

            IEnumerable<ScheduleEvent> GetEvents(DateTime from, DateTime to);
            void Add(ScheduleEvent e);
            void Remove(ScheduleEvent e);
            int RemoveAll(Predicate<ScheduleEvent> predicate);

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
            private static Regex _scheduleLineRegex = new Regex(@"\s*\[([0-9]+)/([0-9]+)\s*\|\s*([0-9?]+):([0-9?]+)\]\s*(.*)", RegexOptions.Compiled);

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

            public static IScheduleMessage TryParse(IUserMessage message)
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
                            var match = _scheduleLineRegex.Match(line);

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

            public static async Task<IScheduleMessage> Create(ITextChannel channel, string header, string footer)
            {
                var content = header + "\n``` ```\n" + footer;
                var message = await channel.SendMessageAsync(content);
                return new DefaultScheduleMessage(message, header, footer);
            }

            public void Add(ScheduleEvent e)
            {
                var where = _events.FindIndex(x => x.Date > e.Date);
                _events.Insert(where < 0 ? _events.Count : where, e);
            }

            public async Task CommitChanges()
            {
                var result = new StringBuilder(Header + "```\n");

                foreach (var e in Events)
                    result.AppendLine(e.Date.ToString(e.HasTime ? @"[MM\/dd | HH:mm]" : @"[MM\/dd | ??:??]") + " " + e.Description);

                result.Append("\n```" + Footer);
                await _message.ModifyAsync(x => x.Content = result.ToString());
            }

            public IEnumerable<ScheduleEvent> GetEvents(DateTime from, DateTime to) => Events.SkipWhile(x => x.Date < from).TakeWhile(x => x.Date < to);
            public bool IsEditable() => _message.Author.Id == ((_message.Channel as ITextChannel)?.Guild as SocketGuild)?.CurrentUser.Id;
            public void Remove(ScheduleEvent e) => _events.Remove(e);
            public int RemoveAll(Predicate<ScheduleEvent> predicate) => _events.RemoveAll(predicate);
        }
    }
}
