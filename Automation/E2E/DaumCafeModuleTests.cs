using System;
using System.Threading.Tasks;
using Disqord.Gateway;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Command matrix for DaumCafeModule (Service/src/DustyBot.Service.New/Modules/DaumCafeModule.cs, the
    /// "cafe" command group). "add"/"remove"/"clear"/"list" require [RequireAuthorContentManager]; the
    /// "credential(s)" submodule has no permission gating at all and its commands are no longer functional
    /// ("add"/"remove" always fail with a "discontinued" message - only "clear"/"list" still do anything).
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class DaumCafeModuleTests : IAsyncLifetime
    {
        private const string InvalidBoardLink = "https://example.com/not-a-cafe-board";

        // From AddCafeFeedAsync's own [Example] - a real, level-restricted public board, so previews aren't
        // accessible without a login and the add is expected to succeed as SuccessWithoutPreviews.
        private const string RealBoardLink = "http://cafe.daum.net/mamamoo/2b6v";

        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ListingTimeout = TimeSpan.FromSeconds(15);

        private readonly ITestOutputHelper _output;

        public DaumCafeModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
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
        public async Task Add_WithRealBoardLink_SucceedsWithoutPreviews()
        {
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}cafe add {RealBoardLink} <#{Fixture.ChannelId}>");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("won't show previews", ack.Content, StringComparison.OrdinalIgnoreCase);

                var result = await RunQueryAsync($"{Options.CommandPrefix}cafe list");
                ResponseAssert.Contains(result, "mamamoo");
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}cafe clear");
            }
        }

        [Fact]
        public async Task Add_InvalidBoardLink_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}cafe add {InvalidBoardLink} <#{Fixture.ChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Unrecognized board link", ack.Content);
        }

        [Fact]
        public async Task Add_TargetingChannelWithoutEmbedPermission_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}cafe add {InvalidBoardLink} <#{Fixture.NoEmbedChannelId}>");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task Add_InvokedByNonContentManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}cafe add {InvalidBoardLink} <#{Fixture.ChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Manage Messages", ack.Content);
        }

        [Fact]
        public async Task Remove_Nonexistent_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}cafe remove {Guid.NewGuid()}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("does not exist", ack.Content);
        }

        [Fact]
        public async Task Remove_InvokedByNonContentManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}cafe remove {Guid.NewGuid()}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Manage Messages", ack.Content);
        }

        [Fact]
        public async Task Clear_Succeeds()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}cafe clear");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task Clear_InvokedByNonContentManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}cafe clear");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Manage Messages", ack.Content);
        }

        [Fact]
        public async Task List_ReturnsResponse()
        {
            await RunQueryAsync($"{Options.CommandPrefix}cafe list");
        }

        [Fact]
        public async Task List_InvokedByNonContentManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}cafe list");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Manage Messages", ack.Content);
        }

        [Fact]
        public async Task CredentialsAdd_AlwaysFails()
        {
            // AddCredentialAsync is [HideInvocation], so its ack is a plain, non-reply message.
            var beforeSend = DateTimeOffset.UtcNow;
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}cafe credential add");
            var ack = await Fixture.Admin.WaitForFollowUpMessageAsync(Options.TargetBotId, Fixture.ChannelId, beforeSend, AckTimeout);
            ResponseAssert.IsFailure(ack);
            Assert.Contains("no longer supported", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CredentialsRemove_AlwaysFails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}cafe credential remove");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("no longer supported", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CredentialsClear_Succeeds()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}cafe credential clear");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task CredentialsClear_InvokedByNonContentManager_StillSucceeds()
        {
            // No permission attribute on the credentials submodule - unlike the main "cafe" commands.
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}cafe credential clear");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task CredentialsList_ReturnsResponse()
        {
            await RunQueryAsync($"{Options.CommandPrefix}cafe credential list");
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        private Task<IGatewayUserMessage> RunLowPrivCommandAsync(string commandText) =>
            Fixture.LowPriv.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        /// <summary>"... list" renders as a paged Table, which isn't reply-linked.</summary>
        private Task<IGatewayUserMessage> RunQueryAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, ListingTimeout);
    }
}
