using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;
using DustyBot.Framework.Utility;
using DustyBot.Service.Definitions;
using DustyBot.Service.Helpers;

namespace DustyBot.Service.Modules
{
    [Module("Polls", "Create public polls.")]
    internal sealed class PollModule
    {
        private readonly ISettingsService _settings;
        private readonly ICommunicator _communicator;
        private readonly IFrameworkReflector _frameworkReflector;
        private readonly HelpBuilder _helpBuilder;

        public PollModule(ISettingsService settings, ICommunicator communicator, IFrameworkReflector frameworkReflector, HelpBuilder helpBuilder)
        {
            _settings = settings;
            _communicator = communicator;
            _frameworkReflector = frameworkReflector;
            _helpBuilder = helpBuilder;
        }

        [Command("poll", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(_helpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
        }

        [Command("poll", "Starts a poll.", CommandFlags.Synchronous)]
        [Alias("poll", "start")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("anonymous", "^anonymous$", ParameterFlags.Optional, "hide the answers")]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Optional, "channel where the poll will take place, uses this channel by default")]
        [Parameter("Question", ParameterType.String, "poll question")]
        [Parameter("Answer1", ParameterType.String, "first answer")]
        [Parameter("Answer2", ParameterType.String, "second answer")]
        [Parameter("MoreAnswers", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Repeatable, "more answers")]
        [Example("#main-chat \"Is hotdog a sandwich?\" Yes No")]
        [Example("anonymous \"Favourite era?\" Hello \"Piano man\" \"Pink Funky\" Melting")]
        public async Task StartPoll(ICommand command)
        {
            if (command["Question"].AsTextChannel != null)
                throw new IncorrectParametersCommandException(string.Empty);

            var channelId = command["Channel"].AsTextChannel?.Id ?? command.Message.Channel.Id;
            var anonymous = command["anonymous"].HasValue;
            var channel = await command.Guild.GetTextChannelAsync(channelId);

            var permissions = (await command.Guild.GetCurrentUserAsync()).GetPermissions(channel);
            if (!permissions.SendMessages)
            {
                await command.ReplyError($"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            if (anonymous && !permissions.ManageMessages)
            {
                await command.ReplyError($"The poll is anonymous but the bot can't delete messages in this channel. Please set the correct guild or channel permissions (Manage Messages).");
                return;
            }

            var poll = new Poll { Channel = channelId, Anonymous = anonymous, Question = command["Question"] };
            poll.Answers.Add(command["Answer1"]);
            poll.Answers.Add(command["Answer2"]);
            poll.Answers.AddRange(command["MoreAnswers"].Repeats.Select(x => x.AsString));

            if ((await _settings.Read<PollSettings>(command.GuildId)).Polls.Any(x => x.Channel == channelId))
            {
                await command.ReplyError("There is already a poll running in this channel. End it before starting a new one.");
                return;
            }

            // Build and send the poll message
            var description = string.Empty;
            for (int i = 0; i < poll.Answers.Count; ++i)
                description += $"`[{i + 1}]` {poll.Answers[i]}\n";

            description += $"\nVote with `{command.Prefix}vote answer` or `{command.Prefix}vote number`.";

            if (description.Length > EmbedBuilder.MaxDescriptionLength)
            {
                await command.ReplyError("This poll is too long (there are too many answers are they are too long to display).");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(poll.Question)
                .WithDescription(description)
                .WithFooter("You may vote again to change your answer");

            await _communicator.SendMessage(channel, embed.Build());

            // Add to settings
            bool added = await _settings.Modify(command.GuildId, (PollSettings s) =>
            {
                if (s.Polls.Any(x => x.Channel == channelId))
                    throw new Exception("Unexpected race condition - poll already running.");

                s.Polls.Add(poll);
                return true;
            });

            if (command.Message.Channel.Id != poll.Channel)
                await command.ReplySuccess("Poll started!");
        }

        [Command("poll", "end", "Ends a poll and announces results.")]
        [Alias("poll", "stop")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Optional, "channel where the poll is taking place, uses current channel if omitted")]
        public async Task EndPoll(ICommand command)
        {
            var channelId = command[0].HasValue ? command[0].AsTextChannel.Id : command.Message.Channel.Id;
            var resultsChannel = command.Message.Channel as ITextChannel;

            bool result = await PrintPollResults(command, true, channelId, resultsChannel);
            if (!result)
                return;

            await _settings.Modify(command.GuildId, (PollSettings s) => s.Polls.RemoveAll(x => x.Channel == channelId) > 0);
        }

        [Command("poll", "results", "Checks results of a running poll.")]
        [Alias("poll", "result")]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Optional, "channel where the poll is taking place, uses current channel if omitted")]
        public async Task ResultsPoll(ICommand command)
        {
            var channelId = command[0].HasValue ? command[0].AsTextChannel.Id : command.Message.Channel.Id;
            var resultsChannel = command.Message.Channel as ITextChannel;

            await PrintPollResults(command, false, channelId, resultsChannel);
        }

        [Command("vote", "Votes in a poll.")]
        [Parameter("Answer", ParameterType.String, ParameterFlags.Remainder, "answer number or a bit of the answer text")]
        [Example("1")]
        [Example("yes")]
        public async Task Vote(ICommand command)
        {
            var poll = await _settings.Modify(command.GuildId, (PollSettings s) =>
            {
                var p = s.Polls.FirstOrDefault(x => x.Channel == command.Message.Channel.Id);
                if (p != null)
                {
                    if (command["Answer"].AsString.All(x => char.IsDigit(x)))
                    {
                        var vote = command["Answer"].AsInt.Value;
                        if (vote > p.Answers.Count || vote < 1)
                            throw new IncorrectParametersCommandException("There is no answer with this number.");

                        p.Votes[command.Message.Author.Id] = vote;
                    }
                    else
                    {
                        var tokens = new string(command["Answer"].AsString.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(new char[] { }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.ToLowerInvariant()).ToList();
                        var scores = new List<int>();
                        foreach (var answerTokens in p.Answers.Select(x => new string(x.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(new char[] { }, StringSplitOptions.RemoveEmptyEntries).Select(y => y.ToLowerInvariant()).ToList()))
                            scores.Add(tokens.Where(x => answerTokens.Contains(x)).Count());

                        if (!scores.Any(x => x > 0))
                            throw new UnclearParametersCommandException($"I don't recognize this answer. Try to vote with `{command.Prefix}vote answer number` instead.", false);

                        var max = scores.Max();
                        if (scores.Where(x => x == max).Count() > 1)
                            throw new UnclearParametersCommandException($"I'm not sure which answer you meant. Try to vote with `{command.Prefix}vote answer number` instead.", false);

                        p.Votes[command.Message.Author.Id] = scores.FindIndex(x => x == max) + 1;
                    }
                }

                return p;
            });
            
            if (poll == null)
            {
                await command.ReplyError("There is no poll running in this channel.");
            }
            else
            {
                var confMessage = await command.ReplySuccess($"**{DiscordHelpers.EscapeMentions("@" + command.Message.Author.Username)}** vote cast.");
                if (poll.Anonymous)
                {
                    await command.Message.DeleteAsync();
                    confMessage.First().DeleteAfter(2);
                }
            }
        }

        private async Task<bool> PrintPollResults(ICommand command, bool closed, ulong channelId, ITextChannel resultsChannel)
        {
            var settings = await _settings.Read<PollSettings>(command.GuildId);
            var poll = settings.Polls.FirstOrDefault(x => x.Channel == channelId);
            if (poll == null)
            {
                await command.ReplyError($"No poll is currently running in the specified channel.");
                return false;
            }

            var user = command.Message.Author as IGuildUser;
            if (user == null)
                throw new Exception("Unexpected context.");

            if (poll.Anonymous && !user.GuildPermissions.ManageMessages)
                throw new MissingPermissionsException("Results of anonymous polls can only be viewed by moderators.", GuildPermission.ManageMessages);

            var description = BuildResultDescription(poll, closed, command.Prefix);

            var total = poll.Results.Sum(x => x.Value);
            var embed = new EmbedBuilder()
                .WithTitle(closed ? "Poll closed!" : "Poll results")
                .WithDescription(description)
                .WithFooter($"{total} vote{(total != 1 ? "s" : "")} total");

            await _communicator.SendMessage(resultsChannel, embed.Build());
            return true;
        }

        private string BuildResultDescription(Poll poll, bool closed, string commandPrefix)
        {
            var description = new StringBuilder(poll.Question + "\n\n");

            var emotes = new Dictionary<int, string>()
            {
                { 1, ":first_place:" },
                { 2, ":second_place:" },
                { 3, ":third_place:" }
            };

            var suffix = closed ? "" : $"\nVote with `{commandPrefix}vote answer` or `{commandPrefix}vote number`.";
            var ellipsis = $"{EmoteConstants.Blank.Name} ...";
            int i = 0, prevScore = int.MaxValue, currentPlace = 0;
            foreach (var result in poll.Results.OrderByDescending(x => x.Value))
            {
                ++i;
                currentPlace = prevScore == result.Value ? currentPlace : i; // Place vote ties in the same placemenet
                prevScore = result.Value;

                var line = $"{(emotes.ContainsKey(currentPlace) && result.Value > 0 ? emotes[currentPlace] : EmoteConstants.Blank.Name)} `[{result.Key}]` **{poll.Answers[result.Key - 1]}** with **{result.Value}** vote{(result.Value != 1 ? "s" : "")}.\n";
                if (description.Length + suffix.Length + line.Length + ellipsis.Length < EmbedBuilder.MaxDescriptionLength)
                {
                    description.Append(line);
                }
                else
                {
                    description.Append(ellipsis);
                    break;
                }
            }

            description.Append(suffix);

            return description.ToString();
        }
    }
}
