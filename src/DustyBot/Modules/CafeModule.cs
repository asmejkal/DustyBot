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
    [Module("Cafe", "Daum Cafe feeds and utility.")]
    class CafeModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }

        public CafeModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }

        private static Regex _daumBoardLinkRegex = new Regex(@"(?:.*cafe.daum.net\/(.+)\/(\w+).*)|(?:.*cafe.daum.net\/(.+)\/bbs_list.+fldid=(\w+).*)", RegexOptions.Compiled);

        [Command("cafe", "add", "Adds a Daum Cafe board feed."), RunAsync]
        [Parameters(ParameterType.String, ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}cafe add DaumCafeBoardLink ChannelMention\n\nDaumCafeBoardLink - link to a public Daum Cafe board section (a standard topic listing board type)")]
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
            feed.LastPostId = await Helpers.DaumCafeSession.Anonymous.GetLastPostId(feed.CafeId, feed.BoardId);

            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                s.DaumCafeFeeds.Add(feed);
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, $"Cafe feed has been added!").ConfigureAwait(false);
        }

        [Command("cafe", "remove", "Removes a Daum Cafe board feed.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}cafe remove FeedId\n\nRun `{p}cafe remove` to see IDs for all active feeds.")]
        public async Task RemoveCafeFeed(ICommand command)
        {
            bool removed = await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                return s.DaumCafeFeeds.RemoveAll(x => x.Id == Guid.Parse((string)command.GetParameter(0))) > 0;
            });

            if (removed)
                await command.ReplySuccess(Communicator, $"Feed has been removed.").ConfigureAwait(false);
            else
                await command.ReplyError(Communicator, $"A feed with this ID does not exist.").ConfigureAwait(false);
        }

        [Command("cafe", "list", "Lists all active Daum Cafe board feeds.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}cafe list")]
        public async Task ListCafeFeeds(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId);

            string result = string.Empty;
            foreach (var feed in settings.DaumCafeFeeds)
                result += $"Id: `{feed.Id}` Board: `{feed.CafeId}/{feed.BoardId}` Channel: `{feed.TargetChannel}`\n";

            if (string.IsNullOrEmpty(result))
                result = "No feeds have been set up. Use the `addCafeFeed` command.";

            await command.Reply(Communicator, result);
        }
    }
}
