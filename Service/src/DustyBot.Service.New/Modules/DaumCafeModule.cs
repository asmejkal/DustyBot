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
using Qmmands.Text;
using Disqord.Bot.Commands;

namespace DustyBot.Service.Modules
{
    [Name("Daum Cafe"), Description("Daum Cafe feeds, including private boards.")]
    [TextGroup("cafe")]
    public class DaumCafeModule : DustyGuildModuleBase
    {
        private readonly IDaumCafeService _service;

        public DaumCafeModule(IDaumCafeService service)
        {
            _service = service;
        }

        [TextCommand("add"), Description("Adds a Daum Cafe board feed."), LongRunning]
        [RequireAuthorContentManager]
        [Remark("**You will not get post previews** for level restricted boards. But if the board is public, the bot will still update with links to new posts.")]
        [Example("http://cafe.daum.net/mamamoo/2b6v #my-channel")]
        public async Task<IDiscordCommandResult> AddCafeFeedAsync(
            [Description("link to a Daum Cafe board section (either a comment board or a forum board), ex. http://cafe.daum.net/mamamoo/2b6v")]
            Uri boardSectionLink,
            [Description("channel or thread that will receive the updates")]
            [RequireBotCanSendEmbeds]
            IMessageGuildChannel channel)
        {
            return await _service.AddCafeFeedAsync(GuildContext.GuildId, Context.Author.Id, boardSectionLink, channel, null, Bot.StoppingToken) switch
            {
                AddCafeFeedResult.Success => Success("Cafe feed has been added!"),
                AddCafeFeedResult.SuccessWithoutPreviews => Success($"Cafe feed has been added!\n{DefaultEmoji.WarningSign} The bot will post updates but it won't show previews because it can't view posts on this board."),
                AddCafeFeedResult.TooManyFeeds => Failure("You've reached the maximum amount of Daum Cafe feeds on this server."),
                AddCafeFeedResult.InvalidBoardLink => Failure("Unrecognized board link."),
                AddCafeFeedResult.InaccessibleBoard => Failure("The bot cannot access this board."),
                AddCafeFeedResult.LoginFailed => Failure("Failed to add this feed."),
                AddCafeFeedResult.CountryBlock => Failure($"Your account is country blocked.\nUnblock it on <https://member.daum.net/security/country.daum>. Allow either all countries (모든 국가 허용) or just the country where the bot is hosted (허용 국가 지정 (최대 5개) -> 추가). Contact the bot owner to get information about the bot's location."),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [TextCommand("remove"), Description("Removes a Daum Cafe board feed.")]
        [RequireAuthorContentManager]
        public async Task<IDiscordCommandResult> RemoveCafeFeedAsync(
            [Description("a feed ID, use `cafe list` to see IDs of all active feeds")]
            Guid feedId)
        {
            return await _service.RemoveCafeFeedAsync(GuildContext.GuildId, feedId, Bot.StoppingToken) switch
            {
                RemoveCafeFeedResult.Success => Success("Feed has been removed."),
                RemoveCafeFeedResult.NotFound => Failure("A feed with this ID does not exist."),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [TextCommand("clear"), Description("Removes all feeds.")]
        [RequireAuthorContentManager]
        public async Task<IDiscordCommandResult> ClearCafeFeedsAsync()
        {
            await _service.ClearCafeFeedsAsync(GuildContext.GuildId, Bot.StoppingToken);
            return Success("All feeds have been removed.");
        }

        [TextCommand("list"), Description("Lists all active Daum Cafe board feeds.")]
        [RequireAuthorContentManager]
        public async Task<IDiscordCommandResult> ListCafeFeedsAsync()
        {
            var feeds = await _service.GetCafeFeedsAsync(GuildContext.GuildId, Bot.StoppingToken);
            return Table(feeds.OrderBy(x => x.CafeId).ThenBy(x => x.BoardId).Select(x => new TableRow()
                .Add("Id", x.Id.ToString())
                .Add("Board", $"{x.CafeId}/{x.BoardId}")
                .Add("Channel", Mention.Channel(x.TargetChannel), TableColumnFlags.Unquoted)
                .Add("Credential", x.CredentialId != default ? x.CredentialId.ToString() : null)));
        }

        [TextGroup("credential", "credentials"), Description("Manage saved Daum accounts used to access private boards. Adding new credentials is no longer supported; existing ones can still be viewed and cleared.")]
        public class CredentialsSubmodule : DustyModuleBase
        {
            private readonly IDaumCafeService _service;

            public CredentialsSubmodule(IDaumCafeService service)
            {
                _service = service;
            }

            [TextCommand("add"), Description("Discontinued. Adding new credentials is no longer supported.")]
            [HideInvocation]
            public IDiscordCommandResult AddCredentialAsync([Remainder] string? args = null)
            {
                return Failure($"Adding new credentials is no longer supported. Existing credentials can still be viewed with `{GetReference(nameof(ListCredentialsAsync))}` and removed with `{GetReference(nameof(ClearCredentialsAsync))}`.");
            }

            [TextCommand("remove"), Description("Discontinued. Removing individual credentials is no longer supported — use `clear` to remove all of them.")]
            public IDiscordCommandResult RemoveCredentialAsync([Remainder] string? args = null)
            {
                return Failure($"Removing individual credentials is no longer supported. Use `{GetReference(nameof(ClearCredentialsAsync))}` to remove all your saved credentials.");
            }

            [TextCommand("clear"), Description("Removes all your saved credentials.")]
            public async Task<IDiscordCommandResult> ClearCredentialsAsync()
            {
                await _service.ClearCredentialsAsync(Context.Author.Id, Bot.StoppingToken);
                return Success("All of your credentials have been removed.");
            }

            [TextCommand("list"), Description("Lists all your saved credentials.")]
            public async Task<IDiscordCommandResult> ListCredentialsAsync()
            {
                var credentials = await _service.GetCredentials(Context.Author.Id, Bot.StoppingToken);
                return Table(credentials.Select(x => new TableRow().Add("Name", x.Name).Add("Id", x.Id.ToString())));
            }
        }
    }
}
