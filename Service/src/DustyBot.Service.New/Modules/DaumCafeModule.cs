using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Interactivity;
using DustyBot.Framework.Modules;
using DustyBot.Service.Communication;
using DustyBot.Service.Services.DaumCafe;
using Qmmands;

namespace DustyBot.Service.Modules
{
    [Name("Daum Cafe"), Description("Daum Cafe feeds, including private boards.")]
    [Group("cafe")]
    public class DaumCafeModule : DustyGuildModuleBase
    {
        private readonly IDaumCafeService _service;

        public DaumCafeModule(IDaumCafeService service)
        {
            _service = service;
        }

        [Command("add"), Description("Adds a Daum Cafe board feed."), LongRunning]
        [RequireAuthorContentManager]
        [Remark("**You will not get post previews** for level restricted boards unless you add a credential. But if the board is public, the bot will still update with links to new posts.")]
        [Remark("Currently only Daum accounts are supported.")]
        [Example("http://cafe.daum.net/mamamoo/2b6v #my-channel")]
        [Example("http://cafe.daum.net/mamamoo/2b6v #my-channel 5a688c9f-72b0-47fa-bbc0-96f82d400a14")]
        public async Task<CommandResult> AddCafeFeedAsync(
            [Description("link to a Daum Cafe board section (either a comment board or a forum board), ex. http://cafe.daum.net/mamamoo/2b6v")]
            Uri boardSectionLink,
            [Description("channel or thread that will receive the updates")]
            [RequireBotCanSendEmbeds]
            IMessageGuildChannel channel,
            [Description("credentials to an account that can view this board (see the `credentials` commands on how to add a credential)")]
            Guid? credentialId)
        {
            return await _service.AddCafeFeedAsync(Context.GuildId, Context.Author.Id, boardSectionLink, channel, credentialId, Bot.StoppingToken) switch
            {
                AddCafeFeedResult.Success => Success("Cafe feed has been added!"),
                AddCafeFeedResult.SuccessWithoutPreviews => Success($"Cafe feed has been added!\n{DefaultEmoji.WarningSign} The bot will post updates but it won't show previews because it can't view posts on this board. To get previews you need to provide credentials to an account that can view posts on this board."),
                AddCafeFeedResult.TooManyFeeds => Failure("You've reached the maximum amount of Daum Cafe feeds on this server."),
                AddCafeFeedResult.InvalidBoardLink => Failure("Unrecognized board link."),
                AddCafeFeedResult.InaccessibleBoard => Failure("The bot cannot access this board. " + (credentialId.HasValue ? "Check if the provided account can view this board." : "You might need to provide credentials to an account that can view this board.")),
                AddCafeFeedResult.LoginFailed => Failure("Failed to login with the supplied credential."),
                AddCafeFeedResult.CountryBlock => Failure($"Your account is country blocked.\nUnblock it on <https://member.daum.net/security/country.daum>. Allow either all countries (모든 국가 허용) or just the country where the bot is hosted (허용 국가 지정 (최대 5개) -> 추가). Contact the bot owner to get information about the bot's location."),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [Command("remove"), Description("Removes a Daum Cafe board feed.")]
        [RequireAuthorContentManager]
        public async Task<CommandResult> RemoveCafeFeedAsync(
            [Description("a feed ID, use `cafe list` to see IDs of all active feeds")]
            Guid feedId)
        {
            return await _service.RemoveCafeFeedAsync(Context.GuildId, feedId, Bot.StoppingToken) switch
            {
                RemoveCafeFeedResult.Success => Success("Feed has been removed."),
                RemoveCafeFeedResult.NotFound => Failure("A feed with this ID does not exist."),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [Command("clear"), Description("Removes all feeds.")]
        [RequireAuthorContentManager]
        public async Task<CommandResult> ClearCafeFeedsAsync()
        {
            await _service.ClearCafeFeedsAsync(Context.GuildId, Bot.StoppingToken);
            return Success("All feeds have been removed.");
        }

        [Command("list"), Description("Lists all active Daum Cafe board feeds.")]
        [RequireAuthorContentManager]
        public async Task<CommandResult> ListCafeFeedsAsync()
        {
            var feeds = await _service.GetCafeFeedsAsync(Context.GuildId, Bot.StoppingToken);
            return Table(feeds.Select(x => new TableRow()
                .Add("Id", x.Id.ToString())
                .Add("Board", $"{x.CafeId}/{x.BoardId}")
                .Add("Channel", Mention.Channel(x.TargetChannel), TableColumnFlags.Unquoted)
                .Add("Credential", x.CredentialId != default ? x.CredentialId.ToString() : null)));
        }

        [Group("credential", "credentials"), Description("To access private boards, the bot requires a Daum account.")]
        [RequireDirectMessage]
        public class CredentialsSubmodule : DustyModuleBase
        {
            private readonly IDaumCafeService _service;

            public CredentialsSubmodule(IDaumCafeService service)
            {
                _service = service;
            }

            [Command("add"), Description("Saves a credential. Direct message only.")]
            [HideInvocation]
            [Remark("Your credentials are stored securely and retrieved by the bot only when necessary.")]
            [Example("johndoe1 mysecretpassword \"Google Mail\"")]
            public async Task<CommandResult> AddCredentialAsync(
            string login,
            string password,
            [Description("type anything for you to recognize these credentials later")]
            string description)
            {
                var id = await _service.AddCredentialAsync(Context.Author.Id, login, password, description, Bot.StoppingToken);
                return Success($"A credential with ID `{id}` has been added! Use `{GetReference(nameof(ListCredentialsAsync))}` to view all your saved credentials.");
            }

            [Command("remove"), Description("Removes a saved credential.")]
            [Example("5a688c9f-72b0-47fa-bbc0-96f82d400a14")]
            public async Task<CommandResult> RemoveCredentialAsync(Guid credentialId)
            {
                return await _service.RemoveCredentialAsync(Context.Author.Id, credentialId, Bot.StoppingToken) switch
                {
                    true => Success("Credential has been removed."),
                    false => Failure($"Couldn't find a credential with this ID. Use `{GetReference(nameof(ListCredentialsAsync))}` to view all your saved credentials and their IDs.")
                };
            }

            [Command("clear"), Description("Removes all your saved credentials.")]
            public async Task<CommandResult> ClearCredentialsAsync()
            {
                await _service.ClearCredentialsAsync(Context.Author.Id, Bot.StoppingToken);
                return Success("All of your credentials have been removed.");
            }

            [Command("list"), Description("Lists all your saved credentials.")]
            public async Task<CommandResult> ListCredentialsAsync()
            {
                var credentials = await _service.GetCredentials(Context.Author.Id, Bot.StoppingToken);
                return Table(credentials.Select(x => new TableRow().Add("Name", x.Name).Add("Id", x.Id.ToString())));
            }
        }
    }
}
