using System.Text.RegularExpressions;
using DustyBot.Framework.Modules;
using Qmmands;

namespace DustyBot.Service.Modules
{
    [Name("Daum Cafe"), Description("Daum Cafe feeds, including private boards.")]
    [Group("cafe")]
    public class DaumCafeModule : DustyGuildModuleBase
    {
        /*[Command("add"), Description("Adds a Daum Cafe board feed.")]
        [RequireAuthorContentManager]
        [Remark("**You will not get post previews** for level restricted boards unless you add a credential. But if the topic listing is public, the bot will still post links to new topics.")]
        [Remark("Currently only Daum accounts are supported.")]
        [Example("http://cafe.daum.net/mamamoo/2b6v #my-channel")]
        [Example("http://cafe.daum.net/mamamoo/2b6v #my-channel 5a688c9f-72b0-47fa-bbc0-96f82d400a14")]
        public async Task AddCafeFeedAsync(
            [Description("link to a Daum Cafe board section (either a comment board or a forum board), ex. http://cafe.daum.net/mamamoo/2b6v")]
            Uri boardSectionLink,
            [Description("channel or thread that will receive the updates")]
            [RequireBotCanSendEmbeds]
            IMessageGuildChannel channel,
            [Description("credentials to an account that can view this board (see `help` for the Credentials module on how to add a credential)")]
            string? credentialId)
        {
            if ((await _settings.Read<YouTubeSettings>(command.GuildId)).DaumCafeFeeds.Count >= ServerFeedLimit)
            {
                await command.ReplyError("You've reached the maximum amount of Daum Cafe feeds on this server.");
                return;
            }

            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError($"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            var feed = new DaumCafeFeed()
            {
                TargetChannel = command["Channel"].AsTextChannel.Id
            };

            bool postsAccesible;
            try
            {
                DaumCafeSession session;
                if (command["CredentialId"].HasValue)
                {
                    feed.CredentialUser = command.Message.Author.Id;
                    feed.CredentialId = (Guid)command["CredentialId"];

                    var credential = await GetCredential(_credentialsService, feed.CredentialUser, feed.CredentialId, ct);
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
                throw new IncorrectParametersCommandException("Unrecognized board link.");
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

            await _settings.Modify(command.GuildId, (YouTubeSettings s) =>
            {
                // Remove duplicate feeds
                s.DaumCafeFeeds.RemoveAll(x => x.CafeId == feed.CafeId && x.BoardId == feed.BoardId && x.TargetChannel == feed.TargetChannel);

                s.DaumCafeFeeds.Add(feed);
            });

            await command.ReplySuccess($"Cafe feed has been added!" + (!postsAccesible ? "\n\n**Warning:** The bot cannot view posts on this board and will not create post previews. To get previews you need to provide credentials to an account that can view posts on this board." : ""));
        }

        [Command("remove"), Description("Removes a Daum Cafe board feed.")]
        [RequireAuthorContentManager]
        public async Task RemoveCafeFeedAsync(
            [Description("a feed ID, use `cafe list` to see IDs of all active feeds")]
            string feedId)
        {
            bool removed = await _settings.Modify(command.GuildId, (YouTubeSettings s) =>
            {
                return s.DaumCafeFeeds.RemoveAll(x => x.Id == (Guid)command["FeedId"]) > 0;
            });

            if (removed)
                await command.ReplySuccess($"Feed has been removed.");
            else
                await command.ReplyError($"A feed with this ID does not exist.");
        }

        [Command("list"), Description("Lists all active Daum Cafe board feeds.")]
        [RequireAuthorContentManager]
        public async Task ListCafeFeedsAsync()
        {
            var settings = await _settings.Read<YouTubeSettings>(command.GuildId);

            string result = string.Empty;
            foreach (var feed in settings.DaumCafeFeeds)
                result += $"Id: `{feed.Id}` Board: `{feed.CafeId}/{feed.BoardId}` Channel: <#{feed.TargetChannel}>" + (feed.CredentialId != Guid.Empty ? $" Credential: `{feed.CredentialId}`\n" : "\n");

            if (string.IsNullOrEmpty(result))
                result = "No feeds have been set up. Use the `cafe add` command.";

            await command.Reply(result);
        }

        [Group("credential", "credentials"), Description("To access private boards, the bot requires a Daum account.")]
        [RequireDirectMessage]
        public class CredentialsSubmodule : DustyModuleBase
        {
            [Command("add"), Description("Saves a credential. Direct message only.")]
            [HideInvocation]
            [Remark("Your credentials are stored in an encrypted database and retrieved by the bot only when necessary.")]
            [Example("johndoe1 mysecretpassword \"Google Mail\"")]
            public async Task AddCredentialAsync(
            string login,
            string password,
            [Description("type anything for you to recognize these credentials later")]
            string description)
            {
                var credentials = new Credentials
                {
                    Login = command["Login"],
                    Password = command["Password"].AsString.ToSecureString(),
                    Name = command["Description"]
                };

                await _credentialsService.AddAsync(command.Author.Id, credentials, ct);
                await command.ReplySuccess($"A credential with ID `{credentials.Id}` has been added! Use `credential list` to view all your saved credentials.");
            }

            [Command("remove"), Description("Removes a saved credential.")]
            [Example("5a688c9f-72b0-47fa-bbc0-96f82d400a14")]
            public async Task RemoveCredentialAsync(string credentialId)
            {
                var removed = await _credentialsService.RemoveAsync(command.Author.Id, (Guid)command["CredentialId"], ct);

                if (removed)
                    await command.ReplySuccess($"Credential has been removed.");
                else
                    await command.ReplyError($"Couldn't find a credential with ID `{command[0]}`. Use `credential list` to view all your saved credentials and their IDs.");
            }

            [Command("clear"), Description("Removes all your saved credentials.")]
            public async Task ClearCredentialsAsync()
            {
                await _credentialsService.ResetAsync(command.Author.Id, ct);
                await command.ReplySuccess($"All your credentials have been removed.");
            }

            [Command("list"), Description("Lists all your saved credentials.")]
            public async Task ListCredential(ICommand command, CancellationToken ct)
            {
                var settings = await _credentialsService.ReadAsync(command.Message.Author.Id, ct);
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
        }*/
    }
}
