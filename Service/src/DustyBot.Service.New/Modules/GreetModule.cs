using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Attributes;
using DustyBot.Framework.Modules;
using Microsoft.Extensions.Logging;
using Qmmands;

namespace DustyBot.Service.Modules
{
    [Group("greet")]
    public class GreetModule : DustyGuildModuleBase
    {
        public const string MentionPlaceholder = "{mention}";
        public const string NamePlaceholder = "{name}";
        public const string FullNamePlaceholder = "{fullname}";
        public const string IdPlaceholder = "{id}";
        public const string ServerPlaceholder = "{server}";
        public const string MemberCountPlaceholder = "{membercount}";

        public const string PlaceholderList = MentionPlaceholder + ", " + NamePlaceholder + ", " + FullNamePlaceholder + ", " + IdPlaceholder + ", " + ServerPlaceholder + ", and " + MemberCountPlaceholder;

        private readonly ISettingsService _settings;
        private readonly ILogger<GreetModule> _logger;

        public GreetModule(
            ISettingsService settings,
            ILogger<GreetModule> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        [Command("text"), Description("Sets a text greeting message.")]
        [Remarks("You can use " + PlaceholderList + "placeholders in the greeting message.")]
        [RequireAuthorAdministrator]
        public async Task<DiscordResponseCommandResult> Greet(
            [Description("a channel that will receive the messages")]
            [RequireBotChannelParameterPermissions(Permission.SendMessages)]
            ITextChannel channel,
            [Description("the greeting message")]
            [Remainder]
            string message)
        {
            await _settings.Modify(Context.GuildId, (EventsSettings s) =>
            {
                s.ResetGreet();
                s.GreetChannel = channel.Id;
                s.GreetMessage = message;
            });

            return Success("Greeting message set.");
        }

        [Command("embed"), Description("Sets an embed greeting message.")]
        [Remarks("You can use " + PlaceholderList + "placeholders in the greeting message.")]
        [RequireAuthorAdministrator]
        public async Task<DiscordResponseCommandResult> Greet(
            [Description("a channel that will receive the messages")]
            [RequireBotChannelParameterPermissions(Permission.SendEmbeds)]
            ITextChannel channel,
            [Description("hex code of a color (e.g. `#09A5BC`)")]
            [Default(null)]
            Color? color,
            [Description("title of the message")]
            string title,
            [Description("body of the greeting message")]
            [Remainder]
            string body)
        {
            await _settings.Modify(Context.GuildId, (EventsSettings s) =>
            {
                s.ResetGreet();
                s.GreetChannel = channel.Id;
                s.GreetEmbed = new GreetEmbed(title, body);

                if (color.HasValue)
                    s.GreetEmbed.Color = (uint)color.Value.RawValue;

                // TODO
                // if (command.Message.Attachments.Any())
                //     s.GreetEmbed.Image = new Uri(command.Message.Attachments.First().Url);
            });

            return Success("Greeting message set.");
        }
    }
}
