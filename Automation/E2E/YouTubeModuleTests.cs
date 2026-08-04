using System;
using System.Threading.Tasks;
using Disqord.Gateway;
using DustyBot.Automation.E2E.Harness;
using Xunit;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Command matrix for YouTubeModule (Service/src/DustyBot.Service.New/Modules/YouTubeModule.cs, the "views"
    /// command group).
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class YouTubeModuleTests : IAsyncLifetime
    {
        private const string SampleVideoId = "qCodzDc61oc";
        private const string SampleVideoUrl = "https://www.youtube.com/watch?v=" + SampleVideoId;
        private const string SampleVideoUrlShort = "https://youtu.be/" + SampleVideoId;

        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ViewsTimeout = TimeSpan.FromSeconds(20); // real YouTube API round trip
        private static readonly RunOnceGate ResetOnce = new();

        private readonly ITestOutputHelper _output;

        // Mutated by tests that rename the song or move its category, so DisposeAsync cleans up the right thing.
        private string _category = "cat" + Guid.NewGuid().ToString("N")[..8];
        private string _songName = "song" + Guid.NewGuid().ToString("N")[..8];

        public YouTubeModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            _output = output;
        }

        private IntegrationTestFixture Fixture { get; }

        private IntegrationTestOptions Options => Fixture.Options;

        public async Task InitializeAsync()
        {
            await ResetOnce.RunOnceAsync(() => Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}views clear"));
            await TestAnnouncer.AnnounceAsync(Fixture.Admin, Fixture.ChannelId, _output);
        }

        public async Task DisposeAsync()
        {
            // Best-effort: each test uses a unique category/song, so this can't affect any other test's data.
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}views remove {_category} {_songName}");
        }

        [Fact]
        public async Task Add_WithoutCategory_UsesDefaultCategoryAndShowsRealStats()
        {
            _category = "default";

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}views add {_songName} {SampleVideoUrl}");
            ResponseAssert.IsSuccess(ack);
            Assert.Contains(_songName, ack.Content);
            Assert.Contains(SampleVideoId, ack.Content);

            // Searches by song name rather than "views" bare - this test is about the default-category
            // assignment, not the bare command's listing; see Views_Bare_ShowsDefaultCategory for that.
            var result = await RunViewsQueryAsync($"{Options.CommandPrefix}views {_songName}");
            ResponseAssert.Contains(result, _songName);
            ResponseAssert.Contains(result, "Views");
        }

        [Fact]
        public async Task Views_Bare_ShowsDefaultCategory()
        {
            _category = "default"; // so DisposeAsync cleans up "views remove default {_songName}"

            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}views add {_songName} {SampleVideoUrl}"));

            var result = await RunViewsQueryAsync($"{Options.CommandPrefix}views");
            ResponseAssert.Contains(result, _songName);
            ResponseAssert.Contains(result, "Views");
        }

        [Fact]
        public async Task Add_WithExplicitCategoryAndMultipleLinks_Succeeds()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}views add {_category} {_songName} {SampleVideoUrl} {SampleVideoUrlShort}");
            ResponseAssert.IsSuccess(ack);

            var result = await RunViewsQueryAsync($"{Options.CommandPrefix}views {_category}");
            ResponseAssert.Contains(result, _songName);
            ResponseAssert.Contains(result, "Views");
        }

        [Fact]
        public async Task Views_ForCategoryWithNoSongs_ReturnsNotFoundMessage()
        {
            var result = await RunViewsQueryAsync($"{Options.CommandPrefix}views {_category}");
            ResponseAssert.Contains(result, "Couldn't find");
        }

        [Fact]
        public async Task List_ShowsAddedSong()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}views add {_category} {_songName} {SampleVideoUrl}"));

            var result = await RunViewsQueryAsync($"{Options.CommandPrefix}views list");
            ResponseAssert.Contains(result, _songName);
            ResponseAssert.Contains(result, _category);
        }

        [Fact]
        public async Task List_InvokedByNonAdmin_StillSucceeds()
        {
            // "list" has no RequireAuthorContentManager attribute, unlike add/remove/clear/rename/move.
            var beforeSend = DateTimeOffset.UtcNow;
            await Fixture.LowPriv.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}views list");
            await Fixture.LowPriv.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, beforeSend, ViewsTimeout);
        }

        [Fact]
        public async Task Remove_ExistingSong_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}views add {_category} {_songName} {SampleVideoUrl}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}views remove {_category} {_songName}");
            ResponseAssert.IsSuccess(ack);

            var result = await RunViewsQueryAsync($"{Options.CommandPrefix}views {_category}");
            ResponseAssert.Contains(result, "Couldn't find");
        }

        [Fact]
        public async Task Remove_NonexistentSong_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}views remove {_category} {_songName}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Couldn't find", ack.Content);
        }

        [Fact]
        public async Task Rename_ExistingSong_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}views add {_category} {_songName} {SampleVideoUrl}"));

            var newName = "song" + Guid.NewGuid().ToString("N")[..8];
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}views rename {_songName} {newName}");
            ResponseAssert.IsSuccess(ack);
            _songName = newName; // so DisposeAsync cleans up the renamed entry

            var result = await RunViewsQueryAsync($"{Options.CommandPrefix}views {_category}");
            ResponseAssert.Contains(result, newName);
        }

        [Fact]
        public async Task Rename_NonexistentSong_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}views rename {_songName} newname");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("no songs", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Move_ExistingCategory_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}views add {_category} {_songName} {SampleVideoUrl}"));

            var newCategory = "cat" + Guid.NewGuid().ToString("N")[..8];
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}views move {_category} {newCategory}");
            ResponseAssert.IsSuccess(ack);
            _category = newCategory; // so DisposeAsync cleans up the moved entry

            var result = await RunViewsQueryAsync($"{Options.CommandPrefix}views {newCategory}");
            ResponseAssert.Contains(result, _songName);
        }

        [Fact]
        public async Task Move_NonexistentCategory_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}views move {_category} othercategory");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("no songs", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Clear_ExistingCategory_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}views add {_category} {_songName} {SampleVideoUrl}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}views clear {_category}");
            ResponseAssert.IsSuccess(ack);

            var result = await RunViewsQueryAsync($"{Options.CommandPrefix}views {_category}");
            ResponseAssert.Contains(result, "Couldn't find");
        }

        [Fact]
        public async Task Clear_EmptyCategory_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}views clear {_category}");
            ResponseAssert.IsFailure(ack);
        }

        [Theory]
        [InlineData("add song https://youtu.be/qCodzDc61oc")]
        [InlineData("remove song")]
        [InlineData("clear category")]
        [InlineData("rename song newname")]
        [InlineData("move category othercategory")]
        public async Task ContentManagerGatedCommands_InvokedByNonAdmin_Fail(string commandSuffix)
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}views {commandSuffix}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("Manage Messages", ack.Content);
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        private Task<IGatewayUserMessage> RunLowPrivCommandAsync(string commandText) =>
            Fixture.LowPriv.RunAndWaitForReplyAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        /// <summary>
        /// "views"/"views list" render as paged menu results (DustyModuleBase.Pages/View), which aren't reliably
        /// reply-linked, so correlation is by timestamp instead (see TestDiscordClient.WaitForAnyMessageAsync).
        /// </summary>
        private Task<IGatewayUserMessage> RunViewsQueryAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, ViewsTimeout);
    }
}
