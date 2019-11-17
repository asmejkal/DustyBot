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
using DustyBot.Framework.Logging;
using DustyBot.Framework.Config;
using DustyBot.Settings;
using DustyBot.Helpers;
using Discord.WebSocket;
using DustyBot.Framework.Exceptions;

namespace DustyBot.Modules
{
    [Module("Poll", "Public polls and surveys.")]
    class PollModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public ILogger Logger { get; private set; }
        public IEssentialConfig Config { get; private set; }

        public PollModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger, IEssentialConfig config)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
            Config = config;
        }

        [Command("poll", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
        }

        [Command("poll", "Starts a poll.")]
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

            var poll = new Poll { Channel = channelId, Anonymous = command["anonymous"].HasValue, Question = command["Question"] };
            poll.Answers.Add(command["Answer1"]);
            poll.Answers.Add(command["Answer2"]);
            poll.Answers.AddRange(command["MoreAnswers"].Repeats.Select(x => x.AsString));

            if ((await Settings.Read<PollSettings>(command.GuildId)).Polls.Any(x => x.Channel == channelId))
            {
                await command.ReplyError(Communicator, "There is already a poll running in this channel. End it before starting a new one.");
                return;
            }

            //Build and send the poll message
            var description = string.Empty;
            for (int i = 0; i < poll.Answers.Count; ++i)
                description += $"`[{i + 1}]` {poll.Answers[i]}\n";

            description += $"\nVote with `{Config.CommandPrefix}vote answer` or `{Config.CommandPrefix}vote number`.";

            if (description.Length > EmbedBuilder.MaxDescriptionLength)
            {
                await command.ReplyError(Communicator, "This poll is too long (there are too many answers are they are too long to display).");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(poll.Question)
                .WithDescription(description)
                .WithFooter("You may vote again to change your answer");
            
            await (await command.Guild.GetTextChannelAsync(channelId)).SendMessageAsync(string.Empty, false, embed.Build());

            //Add to settings
            bool added = await Settings.Modify(command.GuildId, (PollSettings s) =>
            {
                if (s.Polls.Any(x => x.Channel == channelId))
                    throw new Exception("Unexpected race condition - poll already running.");

                s.Polls.Add(poll);
                return true;
            }).ConfigureAwait(false);

            if (command.Message.Channel.Id != poll.Channel)
                await command.ReplySuccess(Communicator, "Poll started!");
        }

        [Command("poll", "end", "Ends a poll and announces results.")]
        [Alias("poll", "stop")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Optional, "channel where the poll is taking place, uses current channel if omitted")]
        public async Task EndPoll(ICommand command)
        {
            var channelId = command[0].HasValue ? command[0].AsTextChannel.Id : command.Message.Channel.Id;
            var resultsChannel = command.Message.Channel as ITextChannel;

            bool result = await PrintPollResults(command, true, channelId, resultsChannel).ConfigureAwait(false);
            if (!result)
                return;

            await Settings.Modify(command.GuildId, (PollSettings s) => s.Polls.RemoveAll(x => x.Channel == channelId) > 0).ConfigureAwait(false);
        }

        [Command("poll", "results", "Checks results of a running poll.")]
        [Alias("poll", "result")]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Optional, "channel where the poll is taking place, uses current channel if omitted")]
        public async Task ResultsPoll(ICommand command)
        {
            var channelId = command[0].HasValue ? command[0].AsTextChannel.Id : command.Message.Channel.Id;
            var resultsChannel = command.Message.Channel as ITextChannel;

            await PrintPollResults(command, false, channelId, resultsChannel).ConfigureAwait(false);
        }

        [Command("vote", "Votes in a poll.")]
        [Parameter("Answer", ParameterType.String, ParameterFlags.Remainder, "answer number or a bit of the answer text")]
        [Example("1")]
        [Example("yes")]
        public async Task Vote(ICommand command)
        {
            var poll = await Settings.Modify(command.GuildId, (PollSettings s) =>
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
                            throw new UnclearParametersCommandException($"I don't recognize this answer. Try to vote with `{Config.CommandPrefix}vote answer number` instead.", false);

                        var max = scores.Max();
                        if (scores.Where(x => x == max).Count() > 1)
                            throw new UnclearParametersCommandException($"I'm not sure which answer you meant. Try to vote with `{Config.CommandPrefix}vote answer number` instead.", false);

                        p.Votes[command.Message.Author.Id] = scores.FindIndex(x => x == max) + 1;
                    }
                }

                return p;
            }).ConfigureAwait(false);
            
            if (poll == null)
                await command.ReplyError(Communicator, "There is no poll running in this channel.").ConfigureAwait(false);
            else
            {
                var confMessage = await command.ReplySuccess(Communicator, $"**{DiscordHelpers.EscapeMentions("@" + command.Message.Author.Username)}** vote cast.").ConfigureAwait(false);
                if (poll.Anonymous)
                {
                    await command.Message.DeleteAsync();
                    confMessage.DeleteAfter(2);
                }
            }
        }

        private async Task<bool> PrintPollResults(ICommand command, bool closed, ulong channelId, ITextChannel resultsChannel)
        {
            var settings = await Settings.Read<PollSettings>(command.GuildId).ConfigureAwait(false);
            var poll = settings.Polls.FirstOrDefault(x => x.Channel == channelId);
            if (poll == null)
            {
                await command.ReplyError(Communicator, $"No poll is currently running in the specified channel.");
                return false;
            }

            var user = command.Message.Author as IGuildUser;
            if (user == null)
                throw new Exception("Unexpected context.");

            if (poll.Anonymous && !user.GuildPermissions.ManageMessages)
                throw new MissingPermissionsException("Results of anonymous polls can only be viewed by moderators.", GuildPermission.ManageMessages);

            var description = BuildResultDescription(poll, closed);

            var total = poll.Results.Sum(x => x.Value);
            var embed = new EmbedBuilder()
                .WithTitle(closed ? "Poll closed!" : "Poll results")
                .WithDescription(description)
                .WithFooter($"{total} vote{(total != 1 ? "s" : "")} total");
            
            await resultsChannel.SendMessageAsync(string.Empty, false, embed.Build());
            return true;
        }

        private string BuildResultDescription(Poll poll, bool closed)
        {
            var description = new StringBuilder(poll.Question + "\n\n");

            var emotes = new Dictionary<int, string>()
            {
                { 1, ":first_place:" },
                { 2, ":second_place:" },
                { 3, ":third_place:" }
            };

            var suffix = closed ? "" : $"\nVote with `{Config.CommandPrefix}vote answer` or `{Config.CommandPrefix}vote number`.";
            var ellipsis = "<:blank:517470655004803072> ...";
            int i = 0, prevScore = int.MaxValue, currentPlace = 0;
            foreach (var result in poll.Results.OrderByDescending(x => x.Value))
            {
                ++i;
                currentPlace = prevScore == result.Value ? currentPlace : i; //Place vote ties in the same placemenet
                prevScore = result.Value;

                var line = $"{(emotes.ContainsKey(currentPlace) && result.Value > 0 ? emotes[currentPlace] : "<:blank:517470655004803072>")} `[{result.Key}]` **{poll.Answers[result.Key - 1]}** with **{result.Value}** vote{(result.Value != 1 ? "s" : "")}.\n";
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
