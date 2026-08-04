using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Command matrix for LogModule ("log" group). Every command requires [RequireAuthorAdministrator]. The
    /// deleted-message log target is a single guild-wide setting, so tests that enable it use their own
    /// temporary channel and disable logging again in a `finally`.
    ///
    /// LogService normally ignores bot-authored messages; real-deletion tests need the tester bots whitelisted
    /// via BotIntegrationOptions, same as DustyBotSharder.OnMessage. A public thread logs like a regular
    /// channel; a private thread only logs for members the bot was added to - both exercised via a thread under
    /// the run channel, left up afterward for review.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class LogModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ListingTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan LogTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan NoLogTimeout = TimeSpan.FromSeconds(8);

        private readonly ITestOutputHelper _output;

        public LogModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            _output = output;
        }

        private IntegrationTestFixture Fixture { get; }

        private IntegrationTestOptions Options => Fixture.Options;

        public async Task InitializeAsync()
        {
            await TestAnnouncer.AnnounceAsync(Fixture.Admin, Fixture.ChannelId, _output);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task EnableMessageLogging_Succeeds()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}log messages <#{Fixture.ChannelId}>");
            ResponseAssert.IsSuccess(ack);

            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log messages disable");
        }

        [Fact]
        public async Task EnableMessageLogging_TargetingChannelWithoutEmbedPermission_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}log messages <#{Fixture.NoEmbedChannelId}>");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task EnableMessageLogging_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}log messages <#{Fixture.ChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("administrator", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DisableMessageLogging_Succeeds()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}log messages disable");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task DisableMessageLogging_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}log messages disable");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("administrator", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PrefixFilter_Add_Succeeds_AndIsListed()
        {
            var prefix = "pfx" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}log prefix filter add {prefix}");
                ResponseAssert.IsSuccess(ack);

                var result = await RunQueryAsync($"{Options.CommandPrefix}log prefix filter list");
                ResponseAssert.Contains(result, prefix);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log prefix filter remove {prefix}");
            }
        }

        [Fact]
        public async Task PrefixFilter_Remove_Succeeds()
        {
            var prefix = "pfx" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}log prefix filter add {prefix}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}log prefix filter remove {prefix}");
            ResponseAssert.IsSuccess(ack);

            var result = await RunQueryAsync($"{Options.CommandPrefix}log prefix filter list");
            ResponseAssert.DoesNotContain(result, prefix);
        }

        [Fact]
        public async Task PrefixFilter_RemoveNonexistent_Fails()
        {
            var prefix = "pfx" + Guid.NewGuid().ToString("N")[..8];
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}log prefix filter remove {prefix}");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task PrefixFilter_Clear_Succeeds()
        {
            var prefix = "pfx" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}log prefix filter add {prefix}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}log prefix filter clear");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task PrefixFilter_Add_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}log prefix filter add somefix");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("administrator", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ChannelFilter_Add_Succeeds_AndIsListed()
        {
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}log channel filter add <#{Fixture.NoSendChannelId}>");
                ResponseAssert.IsSuccess(ack);

                var result = await RunQueryAsync($"{Options.CommandPrefix}log channel filter list");
                ResponseAssert.Contains(result, Fixture.NoSendChannelId.ToString());
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log channel filter remove <#{Fixture.NoSendChannelId}>");
            }
        }

        [Fact]
        public async Task ChannelFilter_Remove_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}log channel filter add <#{Fixture.NoSendChannelId}>"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}log channel filter remove <#{Fixture.NoSendChannelId}>");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task ChannelFilter_Add_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}log channel filter add <#{Fixture.NoSendChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("administrator", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RealMessageDeletion_LogsToConfiguredChannel()
        {
            await using var logChannel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "log-target-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}log messages <#{logChannel.Id}>"));

                var content = "log test message " + Guid.NewGuid().ToString("N")[..8];
                var sent = await Fixture.Admin.SendMessageAsync(Fixture.ChannelId, content);

                var beforeDelete = DateTimeOffset.UtcNow;
                await sent.DeleteAsync();

                var log = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, logChannel.Id, beforeDelete, LogTimeout);
                ResponseAssert.Contains(log, content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log messages disable");
            }
        }

        [Fact]
        public async Task RealMessageDeletion_WithMatchingPrefixFilter_IsNotLogged()
        {
            await using var logChannel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "log-target-" + Guid.NewGuid().ToString("N")[..8]);
            var prefix = "pfx" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}log messages <#{logChannel.Id}>"));
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}log prefix filter add {prefix}"));

                var sent = await Fixture.Admin.SendMessageAsync(Fixture.ChannelId, prefix + " this should not be logged");

                var beforeDelete = DateTimeOffset.UtcNow;
                await sent.DeleteAsync();

                await Assert.ThrowsAsync<TimeoutException>(
                    () => Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, logChannel.Id, beforeDelete, NoLogTimeout));
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log prefix filter remove {prefix}");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log messages disable");
            }
        }

        [Fact]
        public async Task RealMessageDeletion_FromFilteredChannel_IsNotLogged()
        {
            await using var logChannel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "log-target-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}log messages <#{logChannel.Id}>"));
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}log channel filter add <#{Fixture.ChannelId}>"));

                var sent = await Fixture.Admin.SendMessageAsync(Fixture.ChannelId, "this channel is filtered " + Guid.NewGuid().ToString("N")[..8]);

                var beforeDelete = DateTimeOffset.UtcNow;
                await sent.DeleteAsync();

                await Assert.ThrowsAsync<TimeoutException>(
                    () => Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, logChannel.Id, beforeDelete, NoLogTimeout));
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log channel filter remove <#{Fixture.ChannelId}>");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log messages disable");
            }
        }

        [Fact]
        public async Task RealMessageDeletion_InPublicThread_LogsToConfiguredChannel()
        {
            await using var logChannel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "log-target-" + Guid.NewGuid().ToString("N")[..8]);
            // Not deleted afterward - left up alongside the run channel so it can be reviewed in Discord.
            var thread = await Fixture.Admin.Client.CreatePublicThreadAsync(Fixture.ChannelId, "log-thread-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}log messages <#{logChannel.Id}>"));

                var content = "log thread test message " + Guid.NewGuid().ToString("N")[..8];
                var sent = await Fixture.Admin.SendMessageAsync(thread.Id, content);

                var beforeDelete = DateTimeOffset.UtcNow;
                await sent.DeleteAsync();

                var log = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, logChannel.Id, beforeDelete, LogTimeout);
                ResponseAssert.Contains(log, content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log messages disable");
            }
        }

        [Fact]
        public async Task RealMessageDeletion_InPrivateThreadBotNotInvited_IsNotLogged()
        {
            await using var logChannel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "log-target-" + Guid.NewGuid().ToString("N")[..8]);
            // Not deleted afterward - left up alongside the run channel so it can be reviewed in Discord.
            var thread = await Fixture.Admin.Client.CreatePrivateThreadAsync(Fixture.ChannelId, "log-private-thread-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}log messages <#{logChannel.Id}>"));

                // The target bot is never added to this thread, so it can't see (and OnMessageDeleted's own
                // IThreadChannel.CurrentMember check would also reject) anything posted and deleted in it.
                var sent = await Fixture.Admin.SendMessageAsync(thread.Id, "private thread test " + Guid.NewGuid().ToString("N")[..8]);

                var beforeDelete = DateTimeOffset.UtcNow;
                await sent.DeleteAsync();

                await Assert.ThrowsAsync<TimeoutException>(
                    () => Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, logChannel.Id, beforeDelete, NoLogTimeout));
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}log messages disable");
            }
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        private Task<IGatewayUserMessage> RunLowPrivCommandAsync(string commandText) =>
            Fixture.LowPriv.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        /// <summary>"... list" renders as a paged menu (NumberedListing/Table), which isn't reply-linked.</summary>
        private Task<IGatewayUserMessage> RunQueryAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, ListingTimeout);
    }
}
