using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Text;
using Disqord.Gateway;
using DustyBot.Core.Formatting;
using DustyBot.Framework;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Entities;
using DustyBot.Service.Configuration;
using DustyBot.Service.Modules;
using DustyBot.Service.Services.Bot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qmmands;
using Qmmands.Default;
using Qmmands.Text;

namespace DustyBot.Service
{
    public class DustyBotSharder : DustyBotSharderBase
    {
        private readonly HelpBuilder _helpBuilder;
        private readonly IOptions<BotIntegrationOptions> _botIntegrationOptions;

        public DustyBotSharder(
            IOptions<DiscordBotConfiguration> options,
            ILogger<DiscordBot> logger,
            IServiceProvider services,
            DiscordClient client,
            HelpBuilder helpBuilder,
            IOptions<BotIntegrationOptions> botIntegrationOptions)
            : base(options, logger, services, client)
        {
            _helpBuilder = helpBuilder;
            _botIntegrationOptions = botIntegrationOptions;
        }

        protected override ValueTask<bool> OnMessage(IGatewayUserMessage message)
        {
            // Lets explicitly allowed bot accounts drive commands (e.g. the integration test suite's tester
            // bots); AllowedInteractionBotIds is null unless configured, so this is a no-op everywhere else.
            if (_botIntegrationOptions.Value.AllowedInteractionBotIds?.Contains(message.Author.Id) == true)
                return new(true);

            return base.OnMessage(message);
        }

        protected override async ValueTask AddTypeParsers(DefaultTypeParserProvider typeParserProvider, CancellationToken cancellationToken)
        {
            await base.AddTypeParsers(typeParserProvider, cancellationToken);

            // Qmmands' default EnumTypeParser<T> uses case-sensitive Enum.TryParse; commands expect case-insensitive enum arguments (e.g. "av global").
            typeParserProvider.AddParser(new EnumTypeParser<InfoModule.AvatarType>((ReadOnlySpan<char> value, out InfoModule.AvatarType result) => Enum.TryParse(value, ignoreCase: true, out result)));
        }

        protected override void MutateModule(IModuleBuilder moduleBuilder)
        {
            if (moduleBuilder is ITextModuleBuilder textModuleBuilder)
                SetDefaultRateLimits(textModuleBuilder);

            base.MutateModule(moduleBuilder);
        }

        protected override bool FormatFailureMessage(IDiscordCommandContext context, LocalMessageBase message, IResult result)
        {
            if (result is CommandNotFoundResult)
                return false;

            var explanation = result switch
            {
                TypeParseFailedResult x => $"Parameter `{x.Parameter.Name}` is invalid. {x.FailureReason}",
                ChecksFailedResult x => string.Join(' ', x.FailedChecks.Select(x => x.Value.FailureReason)),
                ParameterChecksFailedResult x => $"Parameter `{x.Parameter.Name}` is invalid. "
                    + string.Join(' ', x.FailedChecks.Select(x => x.Value.FailureReason)),
                ExceptionResult => "Oops. Seems that something went wrong...",
                _ => result.FailureReason
            };

            message.Content = $"{CommunicationConstants.FailureMarker} {explanation}";
            message.AllowedMentions = LocalAllowedMentions.None;

            if (message is LocalMessage localMessage)
            {
                if (context is IDiscordTextGuildCommandContext guildContext)
                {
                    var guild = guildContext.Bot.GetGuild(guildContext.GuildId);
                    if (guild is not null && guildContext.Channel is not null && guild.GetBotPermissions(guildContext.Channel).HasFlag(Permissions.ReadMessageHistory))
                        localMessage.WithReply(guildContext.Message.Id);
                }

                if (context is IDiscordTextCommandContext textContext
                    && result is TypeParseFailedResult or ChecksFailedResult or ParameterChecksFailedResult or IArgumentParserResult or OverloadsFailedResult)
                {
                    // Qmmands' MapLookup resets Command to null after a failed attempt whenever other
                    // (even lower-priority, never-actually-tried) matches exist for the input - which includes
                    // the common case of a TextGroup's own command sharing a prefix with its subcommands (e.g.
                    // `views`/`views add`). Path isn't reset the same way, so re-resolve the command through it.
                    var textCommand = context.Command as ITextCommand ?? (textContext.Path is { } path
                        ? Commands.GetCommandMapProvider().GetRequiredMap<ITextCommandMap>()
                            .FindBestMatch(string.Join(' ', path.Reverse()).AsMemory())?.Command
                        : null);

                    if (textCommand != null)
                        localMessage.WithEmbeds(_helpBuilder.BuildCommandUsageEmbed(textCommand, textContext.Prefix));
                }
            }

            return true;
        }

        private static void SetDefaultRateLimits(ITextModuleBuilder moduleBuilder)
        {
            foreach (var submodule in moduleBuilder.Submodules)
            {
                if (submodule is ITextModuleBuilder textSubmodule)
                    SetDefaultRateLimits(textSubmodule);
            }

            foreach (var command in moduleBuilder.Commands)
            {
                if (!command.CustomAttributes.OfType<RateLimitAttribute>().Any(x => x.BucketType is RateLimitBucketType.User))
                {
                    if (command.IsLongRunning())
                        command.CustomAttributes.Add(new RateLimitAttribute(5, 15, RateLimitMeasure.Seconds, RateLimitBucketType.User));
                    else
                        command.CustomAttributes.Add(new RateLimitAttribute(5, 7.5, RateLimitMeasure.Seconds, RateLimitBucketType.User));
                }
            }
        }
    }
}
