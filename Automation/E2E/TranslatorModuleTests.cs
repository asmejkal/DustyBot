using System;
using System.Threading.Tasks;
using Disqord.Gateway;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Command matrix for TranslatorModule (single "translate"/"tr" command, old stack - not yet migrated).
    /// Acks are correlated by timestamp, not reply, since the old framework never reply-links.
    ///
    /// A `TranslatorModule` also exists under `DustyBot.Service.New`, but its `ITranslatorService` dependency is
    /// never registered in that project's `ServiceCollectionExtensions` - it would throw at DI resolution if
    /// invoked, so it isn't actually live; the module running in production is still this old-stack one.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class TranslatorModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(15);

        private readonly ITestOutputHelper _output;

        public TranslatorModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
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
        public async Task Translate_ValidLanguages_Succeeds()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}translate ko en 안녕하세요");

            var embed = Assert.Single(ack.Embeds);
            Assert.Contains("KO", embed.Title, StringComparison.Ordinal);
            Assert.Contains("EN", embed.Title, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(embed.Description));
            Assert.Contains("Papago", embed.Footer?.Text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Translate_ViaTrAlias_Succeeds()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}tr en ko hello");

            var embed = Assert.Single(ack.Embeds);
            Assert.Contains("EN", embed.Title, StringComparison.Ordinal);
            Assert.Contains("KO", embed.Title, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(embed.Description));
        }

        [Fact]
        public async Task Translate_InvokedByNonAdmin_Succeeds()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}translate ko en 감사합니다");

            var embed = Assert.Single(ack.Embeds);
            Assert.False(string.IsNullOrWhiteSpace(embed.Description));
        }

        [Fact]
        public async Task Translate_UnsupportedLanguageCombination_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}translate ko xx hello");

            ResponseAssert.IsFailure(ack);
            Assert.Contains("unsupported language combination", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Translate_InvalidLanguageFormat_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}translate english en hello");

            ResponseAssert.IsFailure(ack);
            Assert.Contains("invalid", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Translate_MissingMessage_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}translate ko en");

            ResponseAssert.IsFailure(ack);
            Assert.Contains("missing", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        private Task<IGatewayUserMessage> RunLowPrivCommandAsync(string commandText) =>
            Fixture.LowPriv.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);
    }
}
