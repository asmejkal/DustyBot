﻿using Discord;
using System;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Commands;
using DustyBot.Settings;
using DustyBot.Helpers;
using DustyBot.Core.Security;
using DustyBot.Database.Services;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;
using DustyBot.Helpers.DaumCafe;
using DustyBot.Helpers.DaumCafe.Exceptions;

namespace DustyBot.Modules
{
    [Module("Daum Cafe", "Daum Cafe feeds, including private boards.")]
    internal sealed class CafeModule
    {
        public const int ServerFeedLimit = 25;

        private readonly ISettingsService _settings;
        private readonly IFrameworkReflector _frameworkReflector;

        public CafeModule(ISettingsService settings, IFrameworkReflector frameworkReflector)
        {
            _settings = settings;
            _frameworkReflector = frameworkReflector;
        }

        [Command("cafe", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("cafe")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(HelpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
        }

        [Command("cafe", "add", "Adds a Daum Cafe board feed.", CommandFlags.TypingIndicator)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("BoardSectionLink", ParameterType.Uri, "link to a Daum Cafe board section (either a comment board or a forum board), ex. http://cafe.daum.net/mamamoo/2b6v")]
        [Parameter("Channel", ParameterType.TextChannel, "channel that will receive the updates")]
        [Parameter("CredentialId", ParameterType.Guid, ParameterFlags.Optional, "credentials to an account that can view this board (see `help` for the Credentials module on how to add a credential)")]
        [Comment("**You will not get post previews** for level restricted boards unless you add a credential. But if the topic listing is public, the bot will still post links to new topics.\n\nCurrently only Daum accounts are supported ")]
        [Example("http://cafe.daum.net/mamamoo/2b6v #my-channel")]
        [Example("http://cafe.daum.net/mamamoo/2b6v #my-channel 5a688c9f-72b0-47fa-bbc0-96f82d400a14")]
        public async Task AddCafeFeed(ICommand command)
        {
            if ((await _settings.Read<MediaSettings>(command.GuildId)).DaumCafeFeeds.Count >= ServerFeedLimit)
            {
                await command.ReplyError("You've reached the maximum amount of Daum Cafe feeds on this server.");
                return;
            }

            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError($"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            var feed = new DaumCafeFeed();
            feed.TargetChannel = command["Channel"].AsTextChannel.Id;

            bool postsAccesible;
            try
            {
                DaumCafeSession session;
                if (command["CredentialId"].HasValue)
                {
                    feed.CredentialUser = command.Message.Author.Id;
                    feed.CredentialId = (Guid)command["CredentialId"];

                    var credential = await GetCredential(_settings, feed.CredentialUser, feed.CredentialId);
                    session = await DaumCafeSession.Create(credential.Login, credential.Password, default);
                }
                else
                {
                    session = DaumCafeSession.Anonymous;
                }

                var info = await session.GetCafeAndBoardId(command["BoardSectionLink"]);
                feed.CafeId = info.Item1;
                feed.BoardId = info.Item2;

                postsAccesible = await session.ArePostsAccesible(feed.CafeId, feed.BoardId, default);
            }
            catch (InvalidBoardLinkException)
            {
                throw new Framework.Exceptions.IncorrectParametersCommandException("Unrecognized board link.");
            }
            catch (InaccessibleBoardException)
            {
                await command.ReplyError("The bot cannot access this board. " + (command.ParametersCount > 2 ? "Check if the provided account can view this board." : "You might need to provide credentials to an account that can view this board."));
                return;
            }
            catch (CountryBlockException)
            {
                await command.ReplyError($"Your account is country blocked.\nUnblock it on <https://member.daum.net/security/country.daum>. Allow either all countries (모든 국가 허용) or just the country where the bot is hosted (허용 국가 지정 (최대 5개) -> 추가). Contact the bot owner to get information about the bot's location.");
                return;
            }
            catch (LoginFailedException)
            {
                await command.ReplyError("Failed to login with the supplied credential.");
                return;
            }

            await _settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                //Remove duplicate feeds
                s.DaumCafeFeeds.RemoveAll(x => x.CafeId == feed.CafeId && x.BoardId == feed.BoardId && x.TargetChannel == feed.TargetChannel);

                s.DaumCafeFeeds.Add(feed);
            });
            
            await command.ReplySuccess($"Cafe feed has been added!" + (!postsAccesible ? "\n\n**Warning:** The bot cannot view posts on this board and will not create post previews. To get previews you need to provide credentials to an account that can view posts on this board." : ""));
        }

        [Command("cafe", "remove", "Removes a Daum Cafe board feed.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("FeedId", ParameterType.Guid)]
        [Comment("Run `cafe list` to see IDs for all active feeds.")]
        public async Task RemoveCafeFeed(ICommand command)
        {
            bool removed = await _settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                return s.DaumCafeFeeds.RemoveAll(x => x.Id == (Guid)command["FeedId"]) > 0;
            });

            if (removed)
                await command.ReplySuccess($"Feed has been removed.");
            else
                await command.ReplyError($"A feed with this ID does not exist.");
        }

        [Command("cafe", "remove", "global", "Removes a Daum Cafe board feed from any server.", CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("FeedId", ParameterType.Guid)]
        public async Task RemoveCafeFeedGlobal(ICommand command)
        {
            foreach (var settings in await _settings.Read<MediaSettings>())
            {
                if (settings.DaumCafeFeeds.Any(x => x.Id == command["FeedId"].AsGuid))
                {
                    var removed = await _settings.Modify(settings.ServerId, (MediaSettings x) => x.DaumCafeFeeds.RemoveAll(y => y.Id == command["FeedId"].AsGuid));

                    await command.ReplySuccess($"Feed has been removed from server `{settings.ServerId}`.");
                    return;
                }
            }

            await command.ReplyError($"A feed with this ID does not exist.");
        }

        [Command("cafe", "list", "Lists all active Daum Cafe board feeds.")]
        [Permissions(GuildPermission.Administrator)]
        public async Task ListCafeFeeds(ICommand command)
        {
            var settings = await _settings.Read<MediaSettings>(command.GuildId);

            string result = string.Empty;
            foreach (var feed in settings.DaumCafeFeeds)
                result += $"Id: `{feed.Id}` Board: `{feed.CafeId}/{feed.BoardId}` Channel: <#{feed.TargetChannel}>" + (feed.CredentialId != Guid.Empty ? $" Credential: `{feed.CredentialId}`\n" : "\n");

            if (string.IsNullOrEmpty(result))
                result = "No feeds have been set up. Use the `cafe add` command.";

            await command.Reply(result);
        }

        [Command("credential", "add", "Saves a credential. Direct message only.", CommandFlags.DirectMessageOnly)]
        [Alias("credentials", "add")]
        [Parameter("Login", ParameterType.String)]
        [Parameter("Password", ParameterType.String)]
        [Parameter("Description", ParameterType.String, "type anything for you to recognize these credentials later")]
        [Comment("Your credentials are stored in an encrypted database and retrieved by the bot only when necessary.")]
        [Example("johndoe1 mysecretpassword \"Google Mail\"")]
        public async Task AddCredential(ICommand command)
        {
            var id = await _settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) =>
            {
                var c = new Credential { Login = command[0], Password = command[1].AsString.ToSecureString(), Name = command[2] };
                s.Credentials.Add(c);
                return c.Id;
            });

            await command.ReplySuccess($"A credential with ID `{id}` has been added! Use `credential list` to view all your saved credentials.");
        }

        [Command("credential", "remove", "Removes a saved credential.", CommandFlags.DirectMessageAllow)]
        [Alias("credentials", "remove")]
        [Parameter("CredentialId", ParameterType.Guid)]
        [Comment("Use `credential list` to view your saved credentials.")]
        [Example("5a688c9f-72b0-47fa-bbc0-96f82d400a14")]
        public async Task RemoveCredential(ICommand command)
        {
            var removed = await _settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) =>
            {
                return s.Credentials.RemoveAll(x => x.Id == (Guid)command[0]) > 0;
            });

            if (removed)
                await command.ReplySuccess($"Credential has been removed.");
            else
                await command.ReplyError($"Couldn't find a credential with ID `{command[0]}`. Use `credential list` to view all your saved credentials and their IDs.");
        }

        [Command("credential", "clear", "Removes all your saved credentials.", CommandFlags.DirectMessageAllow)]
        [Alias("credentials", "clear")]
        public async Task ClearCredential(ICommand command)
        {
            await _settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) => s.Credentials.Clear());

            await command.ReplySuccess($"All your credentials have been removed.");
        }

        [Command("credential", "list", "Lists all your saved credentials.", CommandFlags.DirectMessageAllow)]
        [Alias("credentials", "list")]
        public async Task ListCredential(ICommand command)
        {
            var settings = await _settings.ReadUser<UserCredentials>(command.Message.Author.Id);
            if (settings.Credentials.Count <= 0)
            {
                await command.Reply("No credential saved. Use `credential add` to save a credential.");
                return;
            }

            var result = string.Empty;
            foreach (var credential in settings.Credentials)
            {
                result += $"\nName: `{credential.Name}` Id: `{credential.Id}`";
            }

            await command.Reply(result);
        }

        public static async Task<Credential> GetCredential(ISettingsService settings, ulong userId, string id)
        {
            Guid guid;
            if (!Guid.TryParse(id, out guid))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid ID format. Use `credential list` in a Direct Message to view all your saved credentials and their IDs.");

            return await GetCredential(settings, userId, guid);
        }

        public static async Task<Credential> GetCredential(ISettingsService settings, ulong userId, Guid guid)
        {
            var credentials = await settings.ReadUser<UserCredentials>(userId);
            var credential = credentials.Credentials.FirstOrDefault(x => x.Id == guid);
            if (credential == null)
                throw new Framework.Exceptions.IncorrectParametersCommandException("You don't have a credential saved with this ID. Use `credential add` in a Direct Message to add a credential.");

            return credential;
        }

        public static async Task EnsureCredential(ISettingsService settings, ulong userId, string id)
        {
            Guid guid;
            if (!Guid.TryParse(id, out guid))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid ID format. Use `credential list` to view all your saved credentials and their IDs.");

            var credentials = await settings.ReadUser<UserCredentials>(userId);
            if (!credentials.Credentials.Any(x => x.Id == guid))
                throw new Framework.Exceptions.IncorrectParametersCommandException("You don't have a credential saved with this ID. Use `credential add` in a Direct Message to add a credential.");
        }
    }
}
