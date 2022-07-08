using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Core.Formatting;
using DustyBot.Core.Parsing;
using DustyBot.Database.Mongo.Collections.Reactions;
using DustyBot.Database.Mongo.Collections.Reactions.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Client;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Services;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Services.Reactions
{
    internal class ReactionsService : DustyBotService, IReactionsService
    {
        private readonly ISettingsService _settings;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly Random _rng = new();

        public ReactionsService(ISettingsService settings, IHttpClientFactory httpClientFactory)
        {
            _settings = settings;
            _httpClientFactory = httpClientFactory;
        }

        public Task<int> AddReactionAsync(Snowflake guildId, string trigger, string response, CancellationToken ct)
        {
            return _settings.Modify(guildId, (ReactionsSettings s) =>
            {
                var id = s.NextReactionId++;
                s.Reactions.Add(new Reaction(s.NextReactionId++, trigger, response));
                return id;
            }, ct);
        }

        public Task<EditReactionResult> EditReactionAsync(Snowflake guildId, string idOrTrigger, string response, CancellationToken ct)
        {
            return _settings.Modify(guildId, (ReactionsSettings s) =>
            {
                var reactions = FindReactions(s, idOrTrigger);
                if (!reactions.Any())
                    return EditReactionResult.NotFound;

                if (reactions.Count > 1)
                    return EditReactionResult.AmbiguousQuery;

                reactions.Single().Value = response;
                return EditReactionResult.Success;
            }, ct);
        }

        public Task<int> RenameReactionsAsync(Snowflake guildId, string idOrTrigger, string newTrigger, CancellationToken ct)
        {
            return _settings.Modify(guildId, (ReactionsSettings s) =>
            {
                var reactions = FindReactions(s, idOrTrigger);
                foreach (var reaction in reactions)
                    reaction.Trigger = newTrigger;

                return reactions.Count;
            }, ct);
        }

        public Task<SetCooldownResult> SetCooldownAsync(Snowflake guildId, string idOrTrigger, TimeSpan cooldown, CancellationToken ct)
        {
            if (cooldown < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("Value must be positive", nameof(cooldown));

            return _settings.Modify(guildId, (ReactionsSettings s) =>
            {
                var reactions = FindReactions(s, idOrTrigger);
                if (!reactions.Any())
                    return SetCooldownResult.NotFound;

                foreach (var reaction in reactions)
                    reaction.Cooldown = cooldown;

                return SetCooldownResult.Success;
            }, ct);
        }

        public Task<int> RemoveReactionsAsync(Snowflake guildId, string idOrTrigger, CancellationToken ct)
        {
            return _settings.Modify(guildId, (ReactionsSettings s) =>
            {
                var reactions = FindReactions(s, idOrTrigger);
                s.Reactions = s.Reactions.Except(reactions).ToList();

                return reactions.Count;
            }, ct);
        }

        public Task ClearReactionsAsync(Snowflake guildId, CancellationToken ct)
        {
            return _settings.Modify(guildId, (ReactionsSettings s) =>
            {
                s.Reactions.Clear();
                s.NextReactionId = 1;
            }, ct);
        }

        public async Task<IEnumerable<Reaction>> GetReactionsAsync(Snowflake guildId, CancellationToken ct)
        {
            var settings = await _settings.Read<ReactionsSettings>(guildId, false, ct);
            return settings?.Reactions ?? Enumerable.Empty<Reaction>();
        }

        public async Task<IEnumerable<Reaction>> GetReactionsAsync(Snowflake guildId, string idOrTrigger, CancellationToken ct)
        {
            var settings = await _settings.Read<ReactionsSettings>(guildId, false, ct);
            return FindReactions(settings, idOrTrigger);
        }

        public async Task<IEnumerable<Reaction>> SearchReactionsAsync(Snowflake guildId, string searchInput, CancellationToken ct)
        {
            var settings = await _settings.Read<ReactionsSettings>(guildId, false, ct);
            return settings?.Reactions.Where(x => x.Trigger.Search(searchInput, true) || x.Value.Search(searchInput, true)).ToList()
                ?? Enumerable.Empty<Reaction>();
        }

        public async Task<ReactionStatistics?> GetReactionStatisticsAsync(Snowflake guildId, string idOrTrigger, CancellationToken ct)
        {
            var settings = await _settings.Read<ReactionsSettings>(guildId, false, ct);
            if (settings == null)
                return null;

            var reactions = FindReactions(settings, idOrTrigger);
            if (!reactions.Any())
                return null;

            return new ReactionStatistics(reactions.First().Trigger, reactions.Aggregate(0, (x, y) => x + y.TriggerCount));
        }

        public async Task<IEnumerable<ReactionStatistics>> GetReactionStatisticsAsync(Snowflake guildId, CancellationToken ct)
        {
            var settings = await _settings.Read<ReactionsSettings>(guildId, false, ct);
            return settings?.Reactions.GroupBy(x => x.Trigger, (x, y) => new ReactionStatistics(x, y.Sum(x => x.TriggerCount)))
                ?? Enumerable.Empty<ReactionStatistics>();
        }

        public async Task<Stream?> ExportReactionsAsync(Snowflake guildId, CancellationToken ct)
        {
            var settings = await _settings.Read<ReactionsSettings>(guildId, false, ct);
            if (settings == null || !settings.Reactions.Any())
                return null;

            var reactions = settings.Reactions.Select(x => new Dictionary<string, string>() { { x.Trigger, x.Value } });
            var stream = new MemoryStream();
            try
            {
                await JsonSerializer.SerializeAsync(stream, reactions, new JsonSerializerOptions() { WriteIndented = true });
                stream.Position = 0;
                return stream;
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }
        }

        public async Task<ImportReactionsResult> ImportReactionsAsync(Snowflake guildId, Uri reactionsFileUri, CancellationToken ct)
        {
            using var stream = await _httpClientFactory.CreateClient().GetStreamAsync(reactionsFileUri, ct);
            ICollection<Dictionary<string, string>> reactions;
            try
            {
                reactions = await JsonSerializer.DeserializeAsync<List<Dictionary<string, string>>>(stream)
                    ?? throw new JsonException("Null result");
            }
            catch (JsonException)
            {
                try
                {
                    // Try the second format
                    var result = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream)
                        ?? throw new JsonException("Null result");

                    reactions = new[] { result };
                }
                catch (JsonException)
                {
                    return ImportReactionsResult.InvalidFile;
                }
            }

            await _settings.Modify(guildId, (ReactionsSettings s) =>
            {
                foreach (var reaction in reactions.SelectMany(x => x))
                {
                    var newId = s.NextReactionId++;
                    s.Reactions.Add(new Reaction(newId, reaction.Key, reaction.Value));
                }
            });

            return ImportReactionsResult.Success;
        }

        public async Task<ulong?> GetManagerRoleAsync(Snowflake guildId, CancellationToken ct)
        {
            var settings = await _settings.Read<ReactionsSettings>(guildId, false, ct);
            if (settings == null || settings.ManagerRole == default)
                return null;

            return settings.ManagerRole;
        }

        public Task SetManagerRoleAsync(Snowflake guildId, IRole role, CancellationToken ct)
        {
            return _settings.Modify(guildId, (ReactionsSettings s) => s.ManagerRole = role.Id, ct);
        }

        public Task ResetManagerRoleAsync(Snowflake guildId, CancellationToken ct)
        {
            return _settings.Modify(guildId, (ReactionsSettings s) => s.ManagerRole = default, ct);
        }

        protected override async ValueTask OnMessageReceived(BotMessageReceivedEventArgs e)
        {
            try
            {
                if (e.GuildId == null)
                    return;

                if (e.Message is not IGatewayUserMessage message || message.Author.IsBot)
                    return;

                var settings = await _settings.Read<ReactionsSettings>(e.GuildId.Value, false, Bot.StoppingToken);
                if (settings == null || !settings.Reactions.Any())
                    return;

                var guild = Bot.GetGuildOrThrow(e.GuildId.Value);
                var permissions = guild.GetBotPermissions(e.Channel);
                if (!permissions.SendMessages)
                    return;

                var filtered = settings.Reactions.Where(x => string.Compare(x.Trigger, e.Message.Content, true) == 0).ToList();
                if (filtered.Count <= 0)
                    return;

                var now = DateTimeOffset.UtcNow;
                var cooldownRemaining = filtered.Select(x => x.LastUsage).Max() + filtered.Select(x => x.Cooldown).Max() - now;
                if (cooldownRemaining > TimeSpan.Zero)
                {
                    Logger.WithArgs(e).LogInformation("Triggered reaction cooldown for {ReactionTrigger}", message.Content);
                    var warning = new LocalMessage()
                        .WithContent($"This reaction is on cooldown. Please try again in `{cooldownRemaining.SimpleFormat(TimeSpanPrecision.Medium)}`.");
                        
                    if (permissions.ReadMessageHistory)
                        warning = warning.WithReply(e.MessageId);

                    var botMessage = await e.Channel.SendMessageAsync(warning, cancellationToken: Bot.StoppingToken);
                    if (permissions.ManageMessages)
                    {
                        await Task.Delay(3000);
                        await Task.WhenAll(new[] { message, botMessage }.Select(x => x.DeleteAsync(cancellationToken: Bot.StoppingToken)));
                    }

                    return;
                }

                var reaction = filtered[_rng.Next(filtered.Count)];
                await e.Channel.SendMessageAsync(new LocalMessage().WithContent(reaction.Value), cancellationToken: Bot.StoppingToken);
                await _settings.Modify(e.GuildId.Value, (ReactionsSettings x) =>
                {
                    var y = x.Reactions.First(x => x.Id == reaction.Id);
                    y.TriggerCount++;
                    y.LastUsage = now;
                }, Bot.StoppingToken);

                Logger.WithArgs(e).LogInformation("Triggered reaction {ReactionTrigger} (id: {ReactionId})", message.Content, reaction.Id);
            }
            catch (Exception ex)
            {
                Logger.WithArgs(e).LogError(ex, "Failed to process reaction");
            }
        }

        private static IReadOnlyCollection<Reaction> FindReactions(ReactionsSettings settings, string idOrTrigger)
        {
            if (idOrTrigger.All(x => char.IsDigit(x)) && int.TryParse(idOrTrigger, out var id))
            {
                var reaction = settings.Reactions.FirstOrDefault(x => x.Id == id);
                if (reaction != null)
                    return new[] { reaction };
            }

            return settings.Reactions.Where(x => string.Compare(x.Trigger, idOrTrigger, true) == 0).ToList();
        }
    }
}
