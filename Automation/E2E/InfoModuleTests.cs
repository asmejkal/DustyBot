using System;
using System.Threading.Tasks;
using Disqord.Gateway;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Command matrix for InfoModule (Service/src/DustyBot.Service.New/Modules/InfoModule.cs) - read-only
    /// commands with no permission gating and no persistent state, all rendering via DustyModuleBase.Result
    /// (reply-linked), so every test just calls RunAdminCommandAsync and inspects the resulting embed loosely.
    ///
    /// Banner tests assert the "no banner" text, since neither tester bot account nor the dedicated test guild
    /// is expected to have one configured.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class InfoModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);

        private readonly ITestOutputHelper _output;

        public InfoModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
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
        public async Task Avatar_Bare_ShowsOwnServerAvatar()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}avatar");
            var embed = Assert.Single(result.Embeds);
            Assert.Contains(Fixture.Admin.Client.CurrentUser!.Name, embed.Title);
            Assert.NotNull(embed.Image);
        }

        [Fact]
        public async Task Avatar_WithGlobalType_ShowsGlobalAvatar()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}avatar global");
            var embed = Assert.Single(result.Embeds);
            Assert.NotNull(embed.Image);
        }

        [Fact]
        public async Task Avatar_WithExplicitUser_ShowsThatUsersAvatar()
        {
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}avatar <@{lowPrivId}>");
            var embed = Assert.Single(result.Embeds);
            Assert.Contains(Fixture.LowPriv.Client.CurrentUser!.Name, embed.Title);
        }

        [Fact]
        public async Task Banner_Bare_ReturnsNoBannerMessage()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}banner");
            ResponseAssert.Contains(result, "no profile banner");
        }

        [Fact]
        public async Task Banner_WithExplicitUser_ReturnsNoBannerMessage()
        {
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}banner <@{lowPrivId}>");
            ResponseAssert.Contains(result, "no profile banner");
        }

        [Fact]
        public async Task UserInfo_Bare_ShowsOwnInfo()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}user");
            var embed = Assert.Single(result.Embeds);
            Assert.Contains(Fixture.Admin.Client.CurrentUser!.Id.ToString(), embed.Footer?.Text);
            Assert.Contains(embed.Fields, f => f.Name == "Roles");
            Assert.Contains(embed.Fields, f => f.Name == "Account created on");
        }

        [Fact]
        public async Task UserInfo_WithExplicitUser_ShowsThatUsersInfo()
        {
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}user <@{lowPrivId}>");
            var embed = Assert.Single(result.Embeds);
            Assert.Contains(lowPrivId.ToString(), embed.Footer?.Text);
        }

        [Fact]
        public async Task ServerInfo_ShowsGuildInfo()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}server");
            var embed = Assert.Single(result.Embeds);
            Assert.Contains(Options.GuildId.ToString(), embed.Footer?.Text);
            Assert.Contains(embed.Fields, f => f.Name == "Owner");
            Assert.Contains(embed.Fields, f => f.Name == "Members");
            Assert.Contains(embed.Fields, f => f.Name == "Channels");
            Assert.Contains(embed.Fields, f => f.Name == "Emotes");
        }

        [Fact]
        public async Task ServerBanner_ReturnsNoBannerMessage()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}server banner");
            ResponseAssert.Contains(result, "no banner");
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);
    }
}
