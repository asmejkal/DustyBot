using Discord;
using System;
using System.Linq;
using System.Threading.Tasks;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Settings;
using DustyBot.Settings;
using DustyBot.Helpers;

namespace DustyBot.Modules
{
    [Module("Daum Cafe", "Daum Cafe feeds, including private boards.")]
    class CafeModule : Module
    {
        public const int ServerFeedLimit = 25;

        public ICommunicator Communicator { get; }
        public ISettingsProvider Settings { get; }

        public CafeModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }

        [Command("cafe", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("cafe")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
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
            if ((await Settings.Read<MediaSettings>(command.GuildId)).DaumCafeFeeds.Count >= ServerFeedLimit)
            {
                await command.ReplyError(Communicator, "You've reached the maximum amount of Daum Cafe feeds on this server.");
                return;
            }

            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError(Communicator, $"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
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

                    var credential = await GetCredential(Settings, feed.CredentialUser, feed.CredentialId);
                    session = await DaumCafeSession.Create(credential.Login, credential.Password);
                }
                else
                {
                    session = DaumCafeSession.Anonymous;
                }

                var info = await session.GetCafeAndBoardId(command["BoardSectionLink"]);
                feed.CafeId = info.Item1;
                feed.BoardId = info.Item2;

                postsAccesible = await session.ArePostsAccesible(feed.CafeId, feed.BoardId);
            }
            catch (InvalidBoardLinkException)
            {
                throw new Framework.Exceptions.IncorrectParametersCommandException("Unrecognized board link.");
            }
            catch (InaccessibleBoardException)
            {
                await command.ReplyError(Communicator, "The bot cannot access this board. " + (command.ParametersCount > 2 ? "Check if the provided account can view this board." : "You might need to provide credentials to an account that can view this board."));
                return;
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

            await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                //Remove duplicate feeds
                s.DaumCafeFeeds.RemoveAll(x => x.CafeId == feed.CafeId && x.BoardId == feed.BoardId && x.TargetChannel == feed.TargetChannel);

                s.DaumCafeFeeds.Add(feed);
            }).ConfigureAwait(false);
            
            await command.ReplySuccess(Communicator, $"Cafe feed has been added!" + (!postsAccesible ? "\n\n**Warning:** The bot cannot view posts on this board and will not create post previews. To get previews you need to provide credentials to an account that can view posts on this board." : "")).ConfigureAwait(false);
        }

        [Command("cafe", "remove", "Removes a Daum Cafe board feed.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("FeedId", ParameterType.Guid)]
        [Comment("Run `cafe list` to see IDs for all active feeds.")]
        public async Task RemoveCafeFeed(ICommand command)
        {
            bool removed = await Settings.Modify(command.GuildId, (MediaSettings s) =>
            {
                return s.DaumCafeFeeds.RemoveAll(x => x.Id == (Guid)command["FeedId"]) > 0;
            });

            if (removed)
                await command.ReplySuccess(Communicator, $"Feed has been removed.").ConfigureAwait(false);
            else
                await command.ReplyError(Communicator, $"A feed with this ID does not exist.").ConfigureAwait(false);
        }

        [Command("cafe", "remove", "global", "Removes a Daum Cafe board feed from any server.", CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("FeedId", ParameterType.Guid)]
        public async Task RemoveCafeFeedGlobal(ICommand command)
        {
            foreach (var settings in await Settings.Read<MediaSettings>())
            {
                if (settings.DaumCafeFeeds.Any(x => x.Id == command["FeedId"].AsGuid))
                {
                    var removed = await Settings.Modify(settings.ServerId, (MediaSettings x) => x.DaumCafeFeeds.RemoveAll(y => y.Id == command["FeedId"].AsGuid));

                    await command.ReplySuccess(Communicator, $"Feed has been removed from server `{settings.ServerId}`.").ConfigureAwait(false);
                    return;
                }
            }

            await command.ReplyError(Communicator, $"A feed with this ID does not exist.").ConfigureAwait(false);
        }

        [Command("cafe", "list", "Lists all active Daum Cafe board feeds.")]
        [Permissions(GuildPermission.Administrator)]
        public async Task ListCafeFeeds(ICommand command)
        {
            var settings = await Settings.Read<MediaSettings>(command.GuildId);

            string result = string.Empty;
            foreach (var feed in settings.DaumCafeFeeds)
                result += $"Id: `{feed.Id}` Board: `{feed.CafeId}/{feed.BoardId}` Channel: <#{feed.TargetChannel}>" + (feed.CredentialId != Guid.Empty ? $" Credential: `{feed.CredentialId}`\n" : "\n");

            if (string.IsNullOrEmpty(result))
                result = "No feeds have been set up. Use the `cafe add` command.";

            await command.Reply(Communicator, result);
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
            var id = await Settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) =>
            {
                var c = new Credential { Login = command[0], Password = command[1].AsString.ToSecureString(), Name = command[2] };
                s.Credentials.Add(c);
                return c.Id;
            });

            await command.ReplySuccess(Communicator, $"A credential with ID `{id}` has been added! Use `credential list` to view all your saved credentials.").ConfigureAwait(false);
        }

        [Command("credential", "remove", "Removes a saved credential.", CommandFlags.DirectMessageAllow)]
        [Alias("credentials", "remove")]
        [Parameter("CredentialId", ParameterType.Guid)]
        [Comment("Use `credential list` to view your saved credentials.")]
        [Example("5a688c9f-72b0-47fa-bbc0-96f82d400a14")]
        public async Task RemoveCredential(ICommand command)
        {
            var removed = await Settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) =>
            {
                return s.Credentials.RemoveAll(x => x.Id == (Guid)command[0]) > 0;
            });

            if (removed)
                await command.ReplySuccess(Communicator, $"Credential has been removed.").ConfigureAwait(false);
            else
                await command.ReplyError(Communicator, $"Couldn't find a credential with ID `{command[0]}`. Use `credential list` to view all your saved credentials and their IDs.").ConfigureAwait(false);
        }

        [Command("credential", "clear", "Removes all your saved credentials.", CommandFlags.DirectMessageAllow)]
        [Alias("credentials", "clear")]
        public async Task ClearCredential(ICommand command)
        {
            await Settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) => s.Credentials.Clear());

            await command.ReplySuccess(Communicator, $"All your credentials have been removed.").ConfigureAwait(false);
        }

        [Command("credential", "list", "Lists all your saved credentials.", CommandFlags.DirectMessageAllow)]
        [Alias("credentials", "list")]
        public async Task ListCredential(ICommand command)
        {
            var settings = await Settings.ReadUser<UserCredentials>(command.Message.Author.Id);
            if (settings.Credentials.Count <= 0)
            {
                await command.Reply(Communicator, "No credential saved. Use `credential add` to save a credential.");
                return;
            }

            var result = string.Empty;
            foreach (var credential in settings.Credentials)
            {
                result += $"\nName: `{credential.Name}` Id: `{credential.Id}`";
            }

            await command.Reply(Communicator, result);
        }

        public static async Task<Credential> GetCredential(ISettingsProvider settings, ulong userId, string id)
        {
            Guid guid;
            if (!Guid.TryParse(id, out guid))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid ID format. Use `credential list` in a Direct Message to view all your saved credentials and their IDs.");

            return await GetCredential(settings, userId, guid);
        }

        public static async Task<Credential> GetCredential(ISettingsProvider settings, ulong userId, Guid guid)
        {
            var credentials = await settings.ReadUser<UserCredentials>(userId);
            var credential = credentials.Credentials.FirstOrDefault(x => x.Id == guid);
            if (credential == null)
                throw new Framework.Exceptions.IncorrectParametersCommandException("You don't have a credential saved with this ID. Use `credential add` in a Direct Message to add a credential.");

            return credential;
        }

        public static async Task EnsureCredential(ISettingsProvider settings, ulong userId, string id)
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
