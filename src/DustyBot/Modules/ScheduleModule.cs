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
                await command.ReplyError(Communicator, "No schedule has been set. Use the `schedule add` command.").ConfigureAwait(false);
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

                            var newEvent = new ScheduleEvent
                            {
                                description = match.Groups[5].Value.Trim(),
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
                await command.Reply(Communicator, "No upcoming events.").ConfigureAwait(false);
                return;
            }

            string result = "";
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

            await command.Message.Channel.SendMessageAsync(string.Empty, false, embed.Build()).ConfigureAwait(false);
        }

        [Command("schedule", "channel", "Sets a channel to be used a source for the schedule.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}schedule channel ChannelMention")]
        public async Task SetScheduleChannel(ICommand command)
        {
            if (command.Message.MentionedChannelIds.Count <= 0)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Expected a channel mention.");

            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                s.ScheduleChannel = command.Message.MentionedChannelIds.First();
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, "Schedule channel has been set.").ConfigureAwait(false);
        }

        [Command("schedule", "add", "Adds a message to be used as source for the schedule.")]
        [Parameters(ParameterType.ULong)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}schedule add MessageId\n\nThe specified message will be parsed to be used by the `schedule` command. The expected format is " +
            "following:\n\n[optional text...]```[MM/DD | HH:MM] Event description\n[MM/DD | HH:MM] Another event's description```[optional text...]\n\n" +
            "The HH:MM can be replaced with ??:?? if the event time is unknown.\nAll times in KST.")]
        public async Task AddSchedule(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId).ConfigureAwait(false);
            if (settings.ScheduleChannel == 0)
            {
                await command.ReplyError(Communicator, "Set a schedule channel with `schedule channel` first.").ConfigureAwait(false);
                return;
            }

            //Check if the message exists
            var id = (ulong)command.GetParameter(0);
            var channel = await command.Guild.GetTextChannelAsync(settings.ScheduleChannel).ConfigureAwait(false);
            if (channel == null || await channel.GetMessageAsync(id) == null)
            {
                await command.ReplyError(Communicator, "Couldn't find the specified message.").ConfigureAwait(false);
                return;
            }

            await Settings.Modify(command.GuildId, (MediaSettings s) => s.ScheduleMessages.Add(id)).ConfigureAwait(false);
            await command.ReplySuccess(Communicator, "Schedule message has been added.").ConfigureAwait(false);
        }

        [Command("schedule", "remove", "Removes a message used as schedule source.")]
        [Parameters(ParameterType.ULong)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}schedule remove MessageId")]
        public async Task RemoveSchedule(ICommand command)
        {
            bool removed = await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                return s.ScheduleMessages.Remove((ulong)command.GetParameter(0));
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

        [Command("schedule", "list", "Lists all schedule sources.")]
        [Permissions(GuildPermission.Administrator)]
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
                result += $"Id: `{message}`\n";

            await command.Reply(Communicator, result).ConfigureAwait(false);
        }

        [Command("schedule", "clear", "Removes all schedule sources.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}schedule clear")]
        public async Task ClearSchedule(ICommand command)
        {
            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                s.ScheduleMessages.Clear();
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, $"Schedule has been cleared.").ConfigureAwait(false);
        }
    }
}
