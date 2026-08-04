using System;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Rest;
using Xunit;

namespace DustyBot.Automation.E2E.Harness
{
    /// <summary>
    /// Connects both tester bot accounts once for the whole test run (gateway connects are slow) and creates the
    /// three channels the run needs (normal, no-send, no-embed). The no-send/no-embed channels are deleted
    /// afterwards, but the normal channel is left in place so the run's activity can be reviewed in Discord.
    /// Shared via <see cref="DiscordCollection"/> across every test class - for any module - that drives commands
    /// against the live target bot, so they all reuse the same gateway connections and channels.
    /// </summary>
    public sealed class IntegrationTestFixture : IAsyncLifetime
    {
        public IntegrationTestOptions Options { get; } = IntegrationTestOptions.FromEnvironment();

        public TestDiscordClient Admin { get; private set; } = null!;

        public TestDiscordClient LowPriv { get; private set; } = null!;

        /// <summary>
        /// Normal channel for the run - the target bot can send messages and embeds here. Not deleted after the
        /// run, so the run's activity remains reviewable in Discord; delete old "testrun-" channels manually.
        /// </summary>
        public Snowflake ChannelId { get; private set; }

        /// <summary>The target bot's role is denied Send Messages here, for RequireBotCanSendMessages failures.</summary>
        public Snowflake NoSendChannelId { get; private set; }

        /// <summary>The target bot can send messages but is denied Embed Links here, for RequireBotCanSendEmbeds failures.</summary>
        public Snowflake NoEmbedChannelId { get; private set; }

        public async Task InitializeAsync()
        {
            Admin = await TestDiscordClient.ConnectAsync(Options.AdminTesterBotToken, CancellationToken.None);
            LowPriv = await TestDiscordClient.ConnectAsync(Options.LowPrivTesterBotToken, CancellationToken.None);

            var runId = DateTimeOffset.UtcNow.ToString("yyMMdd-HHmmss");

            var channel = await Admin.Client.CreateTextChannelAsync(Options.GuildId, $"testrun-{runId}");
            ChannelId = channel.Id;

            var noSendChannel = await Admin.Client.CreateTextChannelAsync(Options.GuildId, $"testrun-{runId}-nosend", x =>
                x.Overwrites = new[]
                {
                    new LocalOverwrite(Options.TargetBotId, OverwriteTargetType.Member, new OverwritePermissions(Permissions.None, Permissions.SendMessages)),
                });
            NoSendChannelId = noSendChannel.Id;

            var noEmbedChannel = await Admin.Client.CreateTextChannelAsync(Options.GuildId, $"testrun-{runId}-noembed", x =>
                x.Overwrites = new[]
                {
                    new LocalOverwrite(Options.TargetBotId, OverwriteTargetType.Member, new OverwritePermissions(Permissions.None, Permissions.SendEmbeds)),
                });
            NoEmbedChannelId = noEmbedChannel.Id;
        }

        public async Task DisposeAsync()
        {
            await Admin.Client.DeleteChannelAsync(NoSendChannelId);
            await Admin.Client.DeleteChannelAsync(NoEmbedChannelId);

            await Admin.DisposeAsync();
            await LowPriv.DisposeAsync();
        }
    }

    /// <summary>
    /// All tests in this collection share one guild/channel and are subject to the target bot's per-user rate
    /// limit, so they must run sequentially rather than in xUnit's default per-class parallel execution.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class DiscordCollection : ICollectionFixture<IntegrationTestFixture>
    {
        public const string Name = "Discord";
    }
}
