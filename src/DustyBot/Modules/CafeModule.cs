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
using DustyBot.Helpers;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using System.Threading;

namespace DustyBot.Modules
{
    [Module("Cafe", "Daum Cafe feeds, including private boards.")]
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
        [Usage("{p}cafe add DaumCafeBoardLink ChannelMention [CredentialId]\n\n• *DaumCafeBoardLink* - link to a Daum Cafe board section (either a comment board or a forum board)\n\n• *CredentialId* - optional; credentials to an account that can view this board - see {p}help for the Credentials module on how to add a credential\n\n**You will not get post previews** for level restricted boards unless you add a credential. But if the topic listing is public, the bot will still post links to new topics.\n\n__Examples:__\n{p}cafe add http://cafe.daum.net/mamamoo/2b6v #my-channel\n{p}cafe add http://cafe.daum.net/mamamoo/2b6v #my-channel 5a688c9f-72b0-47fa-bbc0-96f82d400a14")]
        public async Task AddCafeFeed(ICommand command)
        {
            if ((await Settings.Read<MediaSettings>(command.GuildId)).DaumCafeFeeds.Count >= 25)
            {
                await command.ReplyError(Communicator, "You've reached a feed limit for Daum Cafe on this server.");
                return;
            }

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

            //Check accessibilty
            DaumCafeSession.Accessibility accessibility;
            if (command.ParametersCount > 2)
            {
                var credential = await CredentialsModule.GetCredential(Settings, command.Message.Author.Id, (string)command.GetParameter(2));

                try
                {
                    var session = await DaumCafeSession.Create(credential.Login, credential.Password);
                    accessibility = await session.GetBoardAccesibility(feed.CafeId, feed.BoardId);
                }
                catch (CountryBlockException)
                {
                    await command.ReplyError(Communicator, $"Your account is country blocked.\nUnblock it on <https://member.daum.net/security/country.daum>. Allow either all countries (모든 국가 허용) or just the country where the bot is hosted (허용 국가 지정 (최대 5개) -> 추가). Contact the bot owner to get information about the bot's location.");
                    return;
                }
                catch (LoginFailedException)
                {
                    await command.ReplyError(Communicator, "Failed to login with the supplied credential.");
                    return;
                }

                feed.CredentialUser = command.Message.Author.Id;
                feed.CredentialId = Guid.Parse((string)command.GetParameter(2));
            }
            else
            {
                accessibility = await DaumCafeSession.Anonymous.GetBoardAccesibility(feed.CafeId, feed.BoardId);
            }

            if (!accessibility.Topics)
            {
                await command.ReplyError(Communicator, "The bot cannot access this board. " + (command.ParametersCount > 2 ? "Check if the provided account can view this board." : "You might need to provide credentials to an account that can view this board."));
                return;
            }

            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                //Remove duplicate feeds
                s.DaumCafeFeeds.RemoveAll(x => x.CafeId == feed.CafeId && x.BoardId == feed.BoardId && x.TargetChannel == feed.TargetChannel);

                s.DaumCafeFeeds.Add(feed);
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, $"Cafe feed has been added!" + (!accessibility.Posts ? "\n\n**Warning:** The bot cannot view posts on this board and will not create post previews. To get previews you need to provide credentials to an account that can view posts on this board." : "")).ConfigureAwait(false);
        }

        [Command("cafe", "remove", "Removes a Daum Cafe board feed.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}cafe remove FeedId\n\nRun `{p}cafe list` to see IDs for all active feeds.")]
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
                result += $"Id: `{feed.Id}` Board: `{feed.CafeId}/{feed.BoardId}` Channel: `{feed.TargetChannel}`" + (feed.CredentialId != Guid.Empty ? $" Credential: `{feed.CredentialId}`\n" : "\n");

            if (string.IsNullOrEmpty(result))
                result = "No feeds have been set up. Use the `cafe add` command.";

            await command.Reply(Communicator, result);
        }
    }
}
