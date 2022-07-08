using System;
using System.Linq;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Sharding;
using Disqord.Sharding;
using DustyBot.Core.Formatting;
using DustyBot.Framework;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Entities;
using DustyBot.Service.Services.Bot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qmmands;

namespace DustyBot.Service
{
    public class DustyBotSharder : DustyBotSharderBase
    {
        private readonly HelpBuilder _helpBuilder;

        public DustyBotSharder(
            IOptions<DiscordBotSharderConfiguration> options,
            ILogger<DiscordBotSharder> logger,
            IServiceProvider services,
            DiscordClientSharder client,
            HelpBuilder helpBuilder)
            : base(options, logger, services, client)
        {
            _helpBuilder = helpBuilder;
        }

        protected override void MutateModule(ModuleBuilder moduleBuilder)
        {
            SetDefaultCooldowns(moduleBuilder);

            base.MutateModule(moduleBuilder);
        }

        protected override LocalMessage? FormatFailureMessage(DiscordCommandContext context, FailedResult result)
        {
            if (result is CommandNotFoundResult)
                return null;

            var explanation = result switch
            {
                CommandNotFoundResult => null,
                TypeParseFailedResult x => $"Parameter `{x.Parameter}` is invalid. {x.FailureReason}",
                ChecksFailedResult x => string.Join(' ', x.FailedChecks.Select(x => x.Result.FailureReason)),
                ParameterChecksFailedResult x => $"Parameter `{x.Parameter}` is invalid. "
                    + string.Join(' ', x.FailedChecks.Select(x => x.Result.FailureReason)),
                CommandExecutionFailedResult => "Oops. Seems that something went wrong...",
                CommandOnCooldownResult x => $"You're too fast. Please try again in `{x.Cooldowns.Select(x => x.RetryAfter).Max().SimpleFormat()}`.",
                _ => result.FailureReason
            };

            var message = new LocalMessage()
                .WithContent($"{CommunicationConstants.FailureMarker} {explanation}")
                .WithDisallowedMentions();

            if (context is DiscordGuildCommandContext guildContext && guildContext.Guild.GetBotPermissions(guildContext.Channel).ReadMessageHistory)
                message = message.WithReply(context.Message.Id);

            if (result is TypeParseFailedResult or ChecksFailedResult or ParameterChecksFailedResult or ArgumentParseFailedResult or OverloadsFailedResult)
                message = message.WithEmbeds(_helpBuilder.BuildCommandUsageEmbed(context.Command, context.Prefix));

            return message;
        }

        private static void SetDefaultCooldowns(ModuleBuilder moduleBuilder)
        {
            foreach (var submodule in moduleBuilder.Submodules)
                SetDefaultCooldowns(submodule);

            foreach (var command in moduleBuilder.Commands)
            {
                if (!command.Cooldowns.Any(x => x.BucketType is CooldownBucketType.User))
                {
                    if (command.IsLongRunning())
                        command.Cooldowns.Add(new Cooldown(5, TimeSpan.FromSeconds(15), CooldownBucketType.User));
                    else
                        command.Cooldowns.Add(new Cooldown(5, TimeSpan.FromSeconds(7.5), CooldownBucketType.User));
                }
            }
        }
    }
}
