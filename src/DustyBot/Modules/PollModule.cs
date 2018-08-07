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

        [Command("poll", "start", "Starts a poll.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("anonymous", "^anonymous$", ParameterFlags.Optional, "hide the answers")]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Optional, "channel where the poll will take place, uses this channel by default")]
        [Parameter("Question", ParameterType.String, "poll question")]
        [Parameter("Answer1", ParameterType.String, "first answer")]
        [Parameter("Answer2", ParameterType.String, "second answer")]
        [Parameter("MoreAnswers", ParameterType.String, ParameterFlags.Optional | ParameterFlags.Remainder, "more answers")] //TODO
        [Example("#main-chat \"Is hotdog a sandwich?\" Yes No")]
        [Example("anonymous \"Favourite era?\" Hello \"Piano man\" \"Pink Funky\" Melting")]
        public async Task StartPoll(ICommand command)
        {
            //Build up the poll object
            bool anonymous = string.Compare(command[0], "anonymous", true) == 0;
            var channelId = command[anonymous ? 1 : 0].AsTextChannel?.Id ?? command.Message.Channel.Id;
            int optParamsCount = (command[anonymous ? 1 : 0].AsTextChannel != null ? 1 : 0) + (anonymous ? 1 : 0);

            var poll = new Poll { Channel = channelId, Anonymous = anonymous, Question = command[optParamsCount] };
            foreach (var answer in command.GetParameters().Skip(optParamsCount + 1))
                poll.Answers.Add(answer);

            if (poll.Answers.Count < 2)
                throw new Framework.Exceptions.IncorrectParametersCommandException(string.Empty);
            
            //Add to settings
            bool added = await Settings.Modify(command.GuildId, (PollSettings s) =>
            {
                if (s.Polls.Any(x => x.Channel == channelId))
                    return false;
                
                s.Polls.Add(poll);
                return true;
            }).ConfigureAwait(false);

            if (!added)
            {
                await command.ReplyError(Communicator, "There is already a poll running in this channel. End it before starting a new one.");
                return;
            }

            //Build and send the poll message
            var description = string.Empty;
            for (int i = 0; i < poll.Answers.Count; ++i)
                description += $"`{i + 1}.` {poll.Answers[i]}\n";
            
            description += $"\nVote by typing `{Config.CommandPrefix}vote number`.";

            var embed = new EmbedBuilder()
                .WithTitle(poll.Question)
                .WithDescription(description)
                .WithFooter("You may vote again to change your answer");
            
            await (await command.Guild.GetTextChannelAsync(channelId)).SendMessageAsync(string.Empty, false, embed.Build());

            if (command.Message.Channel.Id != poll.Channel)
                await command.ReplySuccess(Communicator, "Poll started!");
        }

        [Command("poll", "end", "Ends a poll and announces results.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("ResultsChannel", ParameterType.TextChannel, ParameterFlags.Optional, "you can specify a different channel to receive the results")]
        public async Task EndPoll(ICommand command)
        {
            var channelId = command.Message.Channel.Id;
            var resultsChannel = command[0].HasValue ? command[0].AsTextChannel : command.Message.Channel as ITextChannel;

            bool result = await PrintPollResults(command, true, channelId, resultsChannel).ConfigureAwait(false);
            if (!result)
                return;

            await Settings.Modify(command.GuildId, (PollSettings s) => s.Polls.RemoveAll(x => x.Channel == channelId) > 0).ConfigureAwait(false);
            if (channelId != resultsChannel.Id)
                await command.ReplySuccess(Communicator, "Poll was ended.").ConfigureAwait(false);
        }

        [Command("poll", "results", "Checks results of a running poll.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("ResultsChannel", ParameterType.TextChannel, ParameterFlags.Optional, "you can specify a different channel to receive the results")]
        public async Task ResultsPoll(ICommand command)
        {
            var channelId = command.Message.Channel.Id;
            var resultsChannel = command[0].HasValue ? command[0].AsTextChannel : command.Message.Channel as ITextChannel;

            await PrintPollResults(command, false, channelId, resultsChannel).ConfigureAwait(false);
        }

        [Command("vote", "Votes in a poll.")]
        [Parameter("AnswerNumber", ParameterType.UInt)]
        [Example("1")]
        public async Task Vote(ICommand command)
        {
            var vote = (int)command.GetParameter(0);
            var poll = await Settings.Modify(command.GuildId, (PollSettings s) =>
            {
                var p = s.Polls.FirstOrDefault(x => x.Channel == command.Message.Channel.Id);
                if (p != null)
                {
                    if (vote > p.Answers.Count || vote < 1)
                        throw new Framework.Exceptions.IncorrectParametersCommandException("There is no answer with this number.");

                    p.Votes[command.Message.Author.Id] = vote;
                }

                return p;
            }).ConfigureAwait(false);
            
            if (poll == null)
                await command.ReplyError(Communicator, "There is no poll running in this channel.").ConfigureAwait(false);
            else
            {
                var confMessage = await command.ReplySuccess(Communicator, $"**{DiscordHelpers.EscapeMention("@" + command.Message.Author.Username)}** vote cast.").ConfigureAwait(false);
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
                await command.ReplyError(Communicator, "No poll is currently running in this channel.");
                return false;
            }

            var description = poll.Question + "\n\n";

            int i = 0;
            foreach (var result in poll.Results.OrderByDescending(x => x.Value))
                description += $"{(++i).ToEnglishOrdinal()} **{poll.Answers[result.Key - 1]}** with **{result.Value}** votes.\n";    

            var embed = new EmbedBuilder()
                .WithTitle(closed ? "Poll closed!" : "Poll results")
                .WithDescription(description)
                .WithFooter($"{poll.Results.Sum(x => x.Value)} votes total");
            
            await resultsChannel.SendMessageAsync(string.Empty, false, embed.Build());
            return true;
        }
    }
}
