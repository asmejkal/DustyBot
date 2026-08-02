using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using DustyBot.Core.Formatting;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Collections.Reactions.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Modules;
using DustyBot.Service.Definitions;
using DustyBot.Service.Services.Log;
using DustyBot.Service.Services.Reactions;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;
using Qmmands.Text;
using Disqord.Bot.Commands;

namespace DustyBot.Service.Modules
{
    [Name("Reactions"), Description("Automatic reactions to messages and custom commands.")]
    [TextGroup("reactions", "reaction")]
    public class ReactionsModule : DustyGuildModuleBase
    {
        public class RequireAuthorReactionsManager : DiscordGuildCheckAttribute
        {
            public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
            {
                if (context.Author.CalculateGuildPermissions().HasFlag(Permissions.ManageMessages))
                    return Results.Success;

                var service = context.Bot.Services.GetRequiredService<IReactionsService>();
                var managerRoleId = await service.GetManagerRoleAsync(context.GuildId, context.Bot.StoppingToken);
                if (managerRoleId.HasValue && context.Author.RoleIds.Contains(managerRoleId.Value))
                    return Results.Success;

                return Results.Failure("Only members with the Manage Messages permission or a reactions manager role can use this command.");
            }
        }

        private readonly IReactionsService _service;

        public ReactionsModule(IReactionsService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [TextCommand("add"), Description("Adds a reaction.")]
        [RequireAuthorReactionsManager]
        [Remark("If you add multiple reactions with the same trigger, one will be randomly chosen each time.")]
        [Example("\"hi bot\" beep boop")]
        public async Task<IDiscordCommandResult> AddReactionAsync(
            [Description("messages that match this will trigger the response")]
            string trigger,
            [Description("the response")]
            [Remainder]
            string response)
        {
            var id = await _service.AddReactionAsync(GuildContext.GuildId, trigger, response, Bot.StoppingToken);
            return Success($"Reaction `{id}` added!");
        }

        [TextCommand("edit"), Description("Edits a reaction.")]
        [RequireAuthorReactionsManager]
        public async Task<IDiscordCommandResult> EditReactionAsync(
            [Description("the reaction trigger or ID from `reactions list`")]
            string idOrTrigger,
            [Description("the new response")]
            [Remainder]
            string response)
        {
            return await _service.EditReactionAsync(GuildContext.GuildId, idOrTrigger, response, Bot.StoppingToken) switch
            {
                EditReactionResult.Success => Success("Reaction was edited!"),
                EditReactionResult.NotFound => Failure("Couldn't find a reaction with this ID or trigger."),
                EditReactionResult.AmbiguousQuery => 
                    ReactionListing(await _service.GetReactionsAsync(GuildContext.GuildId, idOrTrigger, Bot.StoppingToken), "Multiple matches", "Please pick one and run the command again with its ID number"),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [TextCommand("rename"), Description("Changes the trigger of a reaction.")]
        [RequireAuthorReactionsManager]
        public async Task<IDiscordCommandResult> RenameReactionsAsync(
            [Description("the reaction trigger or ID from `reactions list`")]
            string idOrTrigger,
            [Description("the new trigger")]
            [Remainder]
            string newTrigger)
        {
            return await _service.RenameReactionsAsync(GuildContext.GuildId, idOrTrigger, newTrigger, Bot.StoppingToken) switch
            {
                <= 0 => Failure("Couldn't find a reaction with this ID or trigger."),
                1 => Success($"Reaction was edited!"),
                var x => Success($"Edited {x} reactions!")
            };
        }

        [TextCommand("cooldown"), Description("Sets a cooldown for a reaction to prevent spamming.")]
        [RequireAuthorReactionsManager]
        [RequireBotPermissions(Permissions.ManageMessages)]
        [Remark("Cooldown is shared by all reactions with the same trigger.")]
        public async Task<IDiscordCommandResult> SetCooldownAsync(
            [Description("the reaction trigger or ID from `reactions list`")]
            string idOrTrigger,
            [Description("the cooldown in seconds")]
            int cooldown)
        {
            if (cooldown < 1)
                return Failure("Cooldown must be greater than 0.");

            return await _service.SetCooldownAsync(GuildContext.GuildId, idOrTrigger, TimeSpan.FromSeconds(cooldown), Bot.StoppingToken) switch
            {
                SetCooldownResult.Success => Success($"Reactions with this trigger can now be used at most every `{cooldown}` seconds."),
                SetCooldownResult.NotFound => Failure("Couldn't find a reaction with this ID or trigger."),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [TextCommand("remove"), Description("Removes a reaction.")]
        [RequireAuthorReactionsManager]
        public async Task<IDiscordCommandResult> RemoveReactionsAsync(
            [Description("the reaction trigger or ID from `reactions list`")]
            [Remainder]
            string idOrTrigger)
        {
            return await _service.RemoveReactionsAsync(GuildContext.GuildId, idOrTrigger, Bot.StoppingToken) switch
            {
                <= 0 => Failure("Couldn't find a reaction with this ID or trigger."),
                1 => Success($"Reaction was removed."),
                var x => Success($"Removed {x} reactions.")
            };
        }

        [TextCommand("clear"), Description("Removes all reactions.")]
        [RequireAuthorAdministrator]
        public async Task<IDiscordCommandResult> ClearReactionsAsync()
        {
            await _service.ClearReactionsAsync(GuildContext.GuildId, Bot.StoppingToken);
            return Success("All reactions have been cleared.");
        }

        [TextCommand("list"), Description("Lists all reactions.")]
        public async Task<IDiscordCommandResult> ListReactionsAsync()
        {
            var reactions = await _service.GetReactionsAsync(GuildContext.GuildId, Bot.StoppingToken);
            return ReactionListing(reactions, "All reactions", $"{reactions.Count()} reactions");
        }

        [TextCommand("search"), Description("Shows all reactions containing a specific word.")]
        public async Task<IDiscordCommandResult> SearchReactionsAsync(
            [Description("one or more words of the trigger or response")]
            [Remainder]
            string searchInput)
        {
            return (await _service.SearchReactionsAsync(GuildContext.GuildId, searchInput, Bot.StoppingToken)).ToList() switch
            {
                { Count: <= 0 } => Result($"Found no reactions containing `{searchInput}`."),
                var x when x.Count == 1 => ReactionListing(x, "Found 1 reaction"),
                var x => ReactionListing(x, $"Found {x.Count} reactions")
            };
        }

        [TextCommand("stats", "top"), Description("Shows how many times reactions have been used.")]
        public async Task<IDiscordCommandResult> ShowReactionStatsAsync(
            [Description("the reaction trigger or ID; shows stats of all reactions if not specified")]
            [Remainder]
            string? idOrTrigger)
        {
            if (idOrTrigger == null)
            {
                var stats = await _service.GetReactionStatisticsAsync(GuildContext.GuildId, Bot.StoppingToken);
                var items = stats.OrderByDescending(x => x.TriggerCount).ThenBy(x => x.Trigger)
                    .Select(x => $"**{x.Trigger.Truncate(30)}** – triggered **{x.TriggerCount}** times");

                return NumberedListing(items, "Top reactions", x => $"#{x} ");
            }
            else
            {
                return await _service.GetReactionStatisticsAsync(GuildContext.GuildId, idOrTrigger, Bot.StoppingToken) switch
                {
                    null => Result("Couldn't find a reaction with this ID or trigger."),
                    var x => Result($"Triggered **{x.TriggerCount}** times.")
                };
            }
        }

        [TextCommand("export"), Description("Exports all reactions into a file."), LongRunning]
        [RateLimit(1, 1, RateLimitMeasure.Minutes, RateLimitBucketType.Guild)]
        [RequireAuthorReactionsManager]
        public async Task<IDiscordCommandResult> ExportReactionsAsync()
        {
            var stream = await _service.ExportReactionsAsync(GuildContext.GuildId, Bot.StoppingToken);
            return Reply(new LocalMessage()
                .WithContent($"Exported all reactions.")
                .WithAttachments(new LocalAttachment(stream, string.Create(CultureDefinitions.Display, $"Reactions-{Bot.GetGuild(GuildContext.GuildId)?.Name}-{DateTime.UtcNow:yyMMdd-HH-mm)}.json"))));
        }

        [TextCommand("import"), Description("Adds reactions from a file."), LongRunning]
        [RateLimit(1, 1, RateLimitMeasure.Minutes, RateLimitBucketType.Guild)]
        [RequireAuthorReactionsManager]
        [Remark("Attach a file with reactions obtained with `reactions export`. Expected format:")]
        [Remark("`[{\"trigger1\":\"value1\"},{\"trigger2\":\"value2\"}]`")]
        [Remark("")]
        [Remark("Alternative format (doesn't allow duplicates):")]
        [Remark("`{\"trigger1\":\"value1\",\"trigger2\":\"value2\"}`")]
        public async Task<IDiscordCommandResult> ImportReactionsAsync()
        {
            if (!Context.Message.Attachments.Any())
                return Failure($"Please attach a file obtained with `{GetReference(nameof(ExportReactionsAsync))}`.");

            var url = new Uri(Context.Message.Attachments.First().Url);
            return await _service.ImportReactionsAsync(GuildContext.GuildId, url, Bot.StoppingToken) switch
            {
                ImportReactionsResult.Success => Success("Added all reactions!"),
                ImportReactionsResult.InvalidFile => Failure("The provided file is invalid"),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        [VerbCommand("set", "manager"), Description("Sets an optional role that allows users to manage the reactions.")]
        [RequireAuthorAdministrator]
        [Remark("Users with this role will be able to manage the reactions, in addition to users with the Manage Messages privilege.")]
        [Remark("Use without parameters to disable the manager role.")]
        public async Task<IDiscordCommandResult> SetManagerRoleAsync(
            [Description("the role name or ID")]
            [Remainder]
            IRole? role)
        {
            if (role != null)
            {
                await _service.SetManagerRoleAsync(GuildContext.GuildId, role, Bot.StoppingToken);
                return Success($"Users with role `{role.Name}` will now be allowed to manage reactions.");
            }
            else
            {
                await _service.ResetManagerRoleAsync(GuildContext.GuildId, Bot.StoppingToken);
                return Success($"Reactions manager role has been disabled. Users with the Manage Messages permission can still manage reactions.");
            }
        }

        private IDiscordCommandResult ReactionListing(IEnumerable<Reaction> reactions, string title, string? footer = null)
        {
            var fields = reactions
                .OrderBy(x => x.Trigger)
                .ThenBy(x => x.Id)
                .Select(x => new LocalEmbedField()
                    .WithName($"{x.Id}: {x.Trigger}".Truncate(Discord.Limits.Message.Embed.Field.MaxNameLength))
                    .WithValue(x.Value.Truncate(500)));

            return Listing(fields, x =>
            {
                x.WithTitle(title);
                if (footer != null)
                    x.WithFooter(footer);
            });
        }
    }
}
