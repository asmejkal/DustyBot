using System;
using System.Threading.Tasks;
using Disqord.Gateway;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Command matrix for BotModule (Service/src/DustyBot.Service.New/Modules/BotModule.cs). "help" with no
    /// argument or an unrecognized one currently returns a bare Success() - literally no message is sent - so
    /// there is nothing to assert for those two paths; only "help &lt;a known command&gt;" is covered here.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class BotModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan NoResponseTimeout = TimeSpan.FromSeconds(8);

        private readonly ITestOutputHelper _output;

        public BotModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
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
        public async Task Help_WithValidCommand_ShowsCommandHelp()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}help avatar");

            var embed = Assert.Single(result.Embeds);
            Assert.Contains("avatar", embed.Title, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(embed.Fields, f => f.Name == "Usage");
        }

        [Fact]
        public async Task HelpDump_InvokedByNonOwner_Fails()
        {
            var beforeSend = DateTimeOffset.UtcNow;
            await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}help dump");

            try
            {
                var response = await Fixture.LowPriv.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, beforeSend, NoResponseTimeout);
                Assert.Empty(response.Attachments);
            }
            catch (TimeoutException)
            {
                // No response at all is also consistent with the command being rejected.
            }
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);
    }
}
