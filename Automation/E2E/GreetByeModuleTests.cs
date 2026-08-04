using System;
using System.IO;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Http;
using Disqord.Rest;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Full command matrix for GreetByeModule (Service/src/DustyBot.Service.New/Modules/GreetByeModule.cs),
    /// exercised identically against both the "greet" and "bye" command groups via <see cref="GreetCommandTests"/>
    /// and <see cref="ByeCommandTests"/> - the two groups are structurally identical, differing only in wording
    /// and the GreetByeEventType they operate on.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public abstract class GreetByeCommandTestsBase : IAsyncLifetime
    {
        // A minimal valid 1x1 transparent PNG, used to exercise the "attachment instead of image URL" fallback.
        private const string SampleImagePngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

        protected const string SampleImageUrl = "https://i.imgur.com/wSTFkRM.png";

        protected static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        protected static readonly TimeSpan FollowUpTimeout = TimeSpan.FromSeconds(10);

        private readonly ITestOutputHelper _output;

        protected GreetByeCommandTestsBase(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            _output = output;
        }

        protected IntegrationTestFixture Fixture { get; }

        /// <summary>"greet" or "bye" - the command group under test.</summary>
        protected abstract string Group { get; }

        /// <summary>Body text for set commands, worded to match the group under test (a "bye" message shouldn't say "Hi").</summary>
        protected string PlaceholderBody => PlaceholderBodyFor(Group);

        protected static string PlaceholderBodyFor(string group) => group == "greet"
            ? "Hi {mention} ({name}/{fullname}, id {id}) - welcome to {server}! Member #{membercount}."
            : "Bye {mention} ({name}/{fullname}, id {id}) - sorry to see you leave {server}! Member #{membercount}.";

        protected IntegrationTestOptions Options => Fixture.Options;

        public async Task InitializeAsync()
        {
            await TestAnnouncer.AnnounceAsync(Fixture.Admin, Fixture.ChannelId, _output);
            await DisableAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Text_SetAndTrigger_SendsSubstitutedMessage()
        {
            var setAck = await RunAdminCommandAsync(Cmd($"text <#{Fixture.ChannelId}> ") + PlaceholderBody);
            ResponseAssert.IsSuccess(setAck);

            var outcome = await RunTestCommandAsync();
            Assert.True(outcome.Succeeded, "Expected `test` to succeed after setting a text message.");
            AssertPlaceholdersSubstituted(outcome.Message.Content);
        }

        [Fact]
        public async Task Text_TargetingChannelWithoutSendPermission_Fails()
        {
            var ack = await RunAdminCommandAsync(Cmd($"text <#{Fixture.NoSendChannelId}> ") + "Hello there");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task Text_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync(Cmd($"text <#{Fixture.ChannelId}> ") + "Hello there");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("administrator", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Embed_SetAndTrigger_SendsEmbedWithSubstitutedBody()
        {
            var ack = await SetBasicEmbedAsync();
            ResponseAssert.IsSuccess(ack);

            var outcome = await RunTestCommandAsync();
            Assert.True(outcome.Succeeded);
            var embed = Assert.Single(outcome.Message.Embeds);
            AssertPlaceholdersSubstituted(embed.Description);
            Assert.NotNull(embed.Image);
        }

        [Fact]
        public async Task Embed_WithoutColorOrImage_StillSucceeds()
        {
            var ack = await RunAdminCommandAsync(Cmd($"embed <#{Fixture.ChannelId}> ") + PlaceholderBody);
            ResponseAssert.IsSuccess(ack);

            var outcome = await RunTestCommandAsync();
            Assert.True(outcome.Succeeded);
            var embed = Assert.Single(outcome.Message.Embeds);
            Assert.Null(embed.Image);
        }

        [Fact]
        public async Task Embed_WithAttachmentInsteadOfImageParam_UsesAttachmentAsImage()
        {
            await using var stream = new MemoryStream(Convert.FromBase64String(SampleImagePngBase64));
            var content = Cmd($"embed <#{Fixture.ChannelId}> ") + PlaceholderBody;
            var sent = await Fixture.Admin.SendCommandWithAttachmentAsync(Fixture.ChannelId, content, new LocalAttachment(stream, "sample.png"));
            var ack = await Fixture.Admin.WaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, sent.Id, AckTimeout);
            ResponseAssert.IsSuccess(ack);

            var outcome = await RunTestCommandAsync();
            Assert.True(outcome.Succeeded);
            var embed = Assert.Single(outcome.Message.Embeds);
            Assert.NotNull(embed.Image);
        }

        [Fact]
        public async Task Embed_TargetingChannelWithoutEmbedPermission_Fails()
        {
            var ack = await RunAdminCommandAsync(Cmd($"embed <#{Fixture.NoEmbedChannelId}> ") + PlaceholderBody);
            ResponseAssert.IsFailure(ack);
        }

        [Theory]
        [InlineData("title")]
        [InlineData("footer")]
        [InlineData("text")]
        public async Task EmbedSet_BeforeEmbedExists_Fails(string field)
        {
            var ack = await RunAdminCommandAsync(Cmd($"embed set {field} ") + "Some value");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("embed", ack.Content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("first", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("title")]
        [InlineData("footer")]
        [InlineData("text")]
        public async Task EmbedSet_AfterEmbedExists_Succeeds(string field)
        {
            ResponseAssert.IsSuccess(await SetBasicEmbedAsync());

            var ack = await RunAdminCommandAsync(Cmd($"embed set {field} ") + $"Custom {field} value");
            ResponseAssert.IsSuccess(ack);

            var outcome = await RunTestCommandAsync();
            Assert.True(outcome.Succeeded);
            var embed = Assert.Single(outcome.Message.Embeds);
            switch (field)
            {
                case "title":
                    Assert.Contains($"Custom {field} value", embed.Title);
                    break;
                case "footer":
                    Assert.Contains($"Custom {field} value", embed.Footer?.Text);
                    break;
                case "text":
                    Assert.Contains($"Custom {field} value", outcome.Message.Content);
                    break;
            }
        }

        [Theory]
        [InlineData("title")]
        [InlineData("footer")]
        [InlineData("text")]
        public async Task EmbedSet_InvokedByNonAdmin_Fails(string field)
        {
            ResponseAssert.IsSuccess(await SetBasicEmbedAsync());

            var ack = await RunLowPrivCommandAsync(Cmd($"embed set {field} ") + "Attempted value");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("administrator", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("title")]
        [InlineData("footer")]
        [InlineData("text")]
        public async Task EmbedSet_WithNoValue_ClearsField(string field)
        {
            ResponseAssert.IsSuccess(await SetBasicEmbedAsync());
            ResponseAssert.IsSuccess(await RunAdminCommandAsync(Cmd($"embed set {field} ") + "Temporary value"));

            var ack = await RunAdminCommandAsync(Cmd($"embed set {field}"));
            ResponseAssert.IsSuccess(ack);

            var outcome = await RunTestCommandAsync();
            Assert.True(outcome.Succeeded);
            var embed = Assert.Single(outcome.Message.Embeds);
            switch (field)
            {
                case "title":
                    Assert.True(string.IsNullOrEmpty(embed.Title));
                    break;
                case "footer":
                    Assert.Null(embed.Footer);
                    break;
                case "text":
                    Assert.True(string.IsNullOrEmpty(outcome.Message.Content));
                    break;
            }
        }

        [Fact]
        public async Task Test_WithNothingConfigured_Fails()
        {
            var outcome = await RunTestCommandAsync();
            Assert.False(outcome.Succeeded);
            Assert.Contains("first", outcome.Message.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Test_InvokedByNonAdmin_StillSucceeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync(Cmd($"text <#{Fixture.ChannelId}> ") + "Hello there"));

            var sent = await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, Cmd("test"));
            var followUp = await Fixture.LowPriv.WaitForFollowUpMessageAsync(
                Options.TargetBotId, Fixture.ChannelId, sent.Id.CreatedAt, FollowUpTimeout);

            Assert.Equal("Hello there", followUp.Content);
        }

        [Fact]
        public async Task Disable_ThenTest_Fails()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync(Cmd($"text <#{Fixture.ChannelId}> ") + "Hello there"));

            ResponseAssert.IsSuccess(await DisableAsync());

            var outcome = await RunTestCommandAsync();
            Assert.False(outcome.Succeeded);
        }

        [Fact]
        public async Task Disable_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync(Cmd("disable"));
            ResponseAssert.IsFailure(ack);
            Assert.Contains("administrator", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        protected string Cmd(string suffix) => $"{Options.CommandPrefix}{Group} {suffix}";

        private async Task<IGatewayUserMessage> SetBasicEmbedAsync()
        {
            return await RunAdminCommandAsync(Cmd($"embed <#{Fixture.ChannelId}> #09A5BC {SampleImageUrl} ") + PlaceholderBody);
        }

        protected Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        private Task<IGatewayUserMessage> RunLowPrivCommandAsync(string commandText) =>
            Fixture.LowPriv.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        protected Task<IGatewayUserMessage> DisableAsync() => RunAdminCommandAsync(Cmd("disable"));

        /// <summary>
        /// Races the two ways a "test" invocation can resolve: a reply-ack means it failed (see
        /// GreetByeModule.TestGreetAsync/TestByeAsync, Failure(...) when nothing is configured), while a bare
        /// non-reply follow-up message from the target bot means GreetByeSender actually sent the greeting/goodbye.
        /// </summary>
        private async Task<TestOutcome> RunTestCommandAsync()
        {
            var beforeSend = DateTimeOffset.UtcNow;
            var sent = await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, Cmd("test"));

            var replyTask = Fixture.Admin.WaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, sent.Id, AckTimeout);
            var followUpTask = Fixture.Admin.WaitForFollowUpMessageAsync(Options.TargetBotId, Fixture.ChannelId, beforeSend, FollowUpTimeout);

            var completed = await Task.WhenAny(replyTask, followUpTask);
            return completed == replyTask
                ? new TestOutcome(false, await replyTask)
                : new TestOutcome(true, await followUpTask);
        }

        protected static void AssertPlaceholdersSubstituted(string? content)
        {
            Assert.NotNull(content);
            Assert.DoesNotContain("{mention}", content);
            Assert.DoesNotContain("{name}", content);
            Assert.DoesNotContain("{fullname}", content);
            Assert.DoesNotContain("{id}", content);
            Assert.DoesNotContain("{server}", content);
            Assert.DoesNotContain("{membercount}", content);
        }

        private readonly record struct TestOutcome(bool Succeeded, IGatewayUserMessage Message);
    }

    public sealed class GreetCommandTests : GreetByeCommandTestsBase
    {
        public GreetCommandTests(IntegrationTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }

        protected override string Group => "greet";
    }

    public sealed class ByeCommandTests : GreetByeCommandTestsBase
    {
        public ByeCommandTests(IntegrationTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }

        protected override string Group => "bye";

        /// <summary>
        /// Exercises both real trigger paths (GreetByeService.OnMemberLeft/OnMemberJoined -&gt; HandleEventAsync),
        /// not just the "test" command's simulated one, by having the low-privilege tester bot actually leave the
        /// guild via the REST API - there's no dedicated bot for this, so every other test that needs the
        /// low-privilege bot fails until it's re-invited. There is no way to script a bot re-joining a guild
        /// (Discord only allows that through a human clicking "Authorize" on the invite screen), so after leaving,
        /// this test posts the bot's re-invite link and waits (up to <see cref="Options"/>.RejoinTimeout) for a
        /// person to click it - once they do, the resulting MemberJoined event is verified the same way.
        /// </summary>
        [Fact]
        public async Task RealMemberLeaveAndRejoin_TriggersConfiguredByeAndGreetMessages()
        {
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;

            try
            {
                await Fixture.LowPriv.Client.FetchMemberAsync(Options.GuildId, lowPrivId);
            }
            catch (RestApiException ex) when (ex.StatusCode == HttpResponseStatusCode.NotFound)
            {
                Assert.Fail(
                    "The low-privilege tester bot isn't currently in the test guild. Re-invite it using the link " +
                    "this test posted the last time it ran, then run it again.");
                return;
            }

            ResponseAssert.IsSuccess(await RunAdminCommandAsync(Cmd($"text <#{Fixture.ChannelId}> ") + PlaceholderBody));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}greet text <#{Fixture.ChannelId}> " + PlaceholderBodyFor("greet")));

            // A bot's application (client) ID is its user ID, so the re-invite link can be built directly from
            // the bot's own identity instead of requiring a separate config value.
            var inviteUrl = $"https://discord.com/oauth2/authorize?client_id={lowPrivId}&permissions=0&scope=bot" +
                $"&guild_id={Options.GuildId}&disable_guild_select=true";

            await Fixture.LowPriv.SendMessageAsync(
                Fixture.ChannelId,
                "This bot is about to leave the server to test the real \"bye\" trigger. Please re-invite it " +
                $"within {Options.RejoinTimeout.TotalMinutes:0} minutes using this link to also test the real " +
                $"\"greet\" trigger on rejoin: {inviteUrl}");

            var beforeLeave = DateTimeOffset.UtcNow;
            await Fixture.LowPriv.Client.LeaveGuildAsync(Options.GuildId);

            var byeMessage = await Fixture.Admin.WaitForFollowUpMessageAsync(Options.TargetBotId, Fixture.ChannelId, beforeLeave, FollowUpTimeout);
            AssertPlaceholdersSubstituted(byeMessage.Content);
            Assert.Contains(lowPrivId.ToString(), byeMessage.Content);

            var afterBye = DateTimeOffset.UtcNow;
            var greetMessage = await Fixture.Admin.WaitForFollowUpMessageAsync(Options.TargetBotId, Fixture.ChannelId, afterBye, Options.RejoinTimeout);
            AssertPlaceholdersSubstituted(greetMessage.Content);
            Assert.Contains(lowPrivId.ToString(), greetMessage.Content);
        }
    }

    [Collection(DiscordCollection.Name)]
    public sealed class GreetByeCrossCuttingTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;

        public GreetByeCrossCuttingTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            _output = output;
        }

        private IntegrationTestFixture Fixture { get; }

        private IntegrationTestOptions Options => Fixture.Options;

        public async Task InitializeAsync()
        {
            await TestAnnouncer.AnnounceAsync(Fixture.Admin, Fixture.ChannelId, _output);
            await DisableAsync("greet");
            await DisableAsync("bye");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SettingGreetDoesNotConfigureBye()
        {
            var setAck = await RunAdminCommandAsync($"{Options.CommandPrefix}greet text <#{Fixture.ChannelId}> Welcome!");
            ResponseAssert.IsSuccess(setAck);

            var byeAck = await RunAdminCommandAsync($"{Options.CommandPrefix}bye test");
            ResponseAssert.IsFailure(byeAck);
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, TimeSpan.FromSeconds(10));

        private Task DisableAsync(string group) => RunAdminCommandAsync($"{Options.CommandPrefix}{group} disable");
    }
}
