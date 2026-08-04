using System;
using System.Linq;
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
    /// Command matrix for ScheduleModule ("schedule"/"event"/"calendar" groups, old stack - not yet migrated).
    /// Acks are correlated by timestamp, not reply, since the old framework never reply-links.
    ///
    /// Most mutating commands are gated by the module's own AssertPrivileges check (ManageMessages OR a
    /// configurable role), not the framework's [Permissions] attribute - tested once rather than per command,
    /// since they share the same code path. Three commands are additionally [Permissions(Administrator)]-gated.
    ///
    /// Dates are computed relative to "now" so tests stay valid regardless of when they run. Events/calendars
    /// are guild-wide lists, not per-test isolated - each test removes what it added and restores any singleton
    /// setting it changed.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class ScheduleModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);

        private readonly ITestOutputHelper _output;

        public ScheduleModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
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

        private static string FutureDate(int daysFromNow) => DateTimeOffset.UtcNow.AddDays(daysFromNow).ToString("yyyy/MM/dd");

        [Fact]
        public async Task AssertPrivileges_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}event add {FutureDate(30)} nonmanager test");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetScheduleRole_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}schedule set manager");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetScheduleNotifications_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}schedule set notifications <#{Fixture.ChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Schedule_WithNoEvents_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule nonexistent-tag-{Guid.NewGuid():N}");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task Schedule_WithEvents_ShowsThem()
        {
            var description = "sched-test-" + Guid.NewGuid().ToString("N")[..8];
            var addAck = await RunAdminCommandAsync($"{Options.CommandPrefix}event add {FutureDate(10)} {description}");
            ResponseAssert.IsSuccess(addAck);
            var id = ExtractEventId(addAck.Content);
            try
            {
                var result = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule");
                var embed = Assert.Single(result.Embeds);
                Assert.Contains(description, embed.Description);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event remove {id}");
            }
        }

        [Fact]
        public async Task SetScheduleStyle_Succeeds_ThenResetsToDefault()
        {
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule set style KoreanDate");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("KoreanDate", ack.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}schedule set style Default");
            }
        }

        [Fact]
        public async Task SetScheduleStyle_InvalidFormat_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule set style NotAFormat");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("unknown event formatting type", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetScheduleRole_Succeeds_ThenDisables()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "sched-mgr-" + Guid.NewGuid().ToString("N")[..8]);
            var setAck = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule set manager {role.Id}");
            ResponseAssert.IsSuccess(setAck);
            Assert.Contains(role.Id.ToString(), setAck.Content);

            var disableAck = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule set manager");
            ResponseAssert.IsSuccess(disableAck);
            Assert.Contains("disabled", disableAck.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetScheduleNotifications_Succeeds_ThenReset()
        {
            var tag = "notif" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                var setAck = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule set notifications {tag} <#{Fixture.ChannelId}>");
                ResponseAssert.IsSuccess(setAck);
            }
            finally
            {
                var resetAck = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule reset notifications {tag}");
                ResponseAssert.IsSuccess(resetAck);
            }
        }

        [Fact]
        public async Task SetScheduleNotifications_TargetChannelWithoutSendPermission_Fails()
        {
            var tag = "notif" + Guid.NewGuid().ToString("N")[..8];
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule set notifications {tag} <#{Fixture.NoSendChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("can't send messages", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ResetScheduleNotifications_NotConfigured_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule reset notifications nonexistent-{Guid.NewGuid():N}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("no notification settings", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetScheduleTimezone_Succeeds_ThenResetsToDefault()
        {
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule set timezone UTC-5");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("UTC-5", ack.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}schedule set timezone UTC+9");
            }
        }

        [Fact]
        public async Task SetScheduleTimezone_OutOfRange_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule set timezone UTC+15");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("unknown timezone", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetScheduleLength_Succeeds_ThenResetsToDefault()
        {
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule set length 5");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("5", ack.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}schedule set length 15");
            }
        }

        [Fact]
        public async Task SetScheduleLength_ZeroOrLess_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule set length 0");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("more than 0", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RenameScheduleTag_Succeeds()
        {
            var oldTag = "oldtag" + Guid.NewGuid().ToString("N")[..8];
            var newTag = "newtag" + Guid.NewGuid().ToString("N")[..8];
            var addAck = await RunAdminCommandAsync($"{Options.CommandPrefix}event add {oldTag} {FutureDate(10)} rename-test-{Guid.NewGuid():N}");
            ResponseAssert.IsSuccess(addAck);
            var id = ExtractEventId(addAck.Content);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule rename tag {oldTag} {newTag}");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("retagged", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event remove {id}");
            }
        }

        [Fact]
        public async Task RenameScheduleTag_ReservedTargetTag_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule rename tag sometag notify");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("reserved", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RenameScheduleTag_NoMatchingEvents_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule rename tag nonexistent-{Guid.NewGuid():N} newtag");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("found no events", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExportSchedule_WithEvents_ReturnsAttachment()
        {
            var addAck = await RunAdminCommandAsync($"{Options.CommandPrefix}event add {FutureDate(10)} export-test-{Guid.NewGuid():N}");
            ResponseAssert.IsSuccess(addAck);
            var id = ExtractEventId(addAck.Content);
            try
            {
                var result = await RunAdminCommandAsync($"{Options.CommandPrefix}schedule export");
                Assert.NotEmpty(result.Attachments);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event remove {id}");
            }
        }

        [Fact]
        public async Task AddEvent_Succeeds_AndIsSearchable()
        {
            var description = "add-test-" + Guid.NewGuid().ToString("N")[..8];
            var addAck = await RunAdminCommandAsync($"{Options.CommandPrefix}event add {FutureDate(10)} {description}");
            ResponseAssert.IsSuccess(addAck);
            var id = ExtractEventId(addAck.Content);
            try
            {
                var searchResult = await RunAdminCommandAsync($"{Options.CommandPrefix}event search {description}");
                ResponseAssert.Contains(searchResult, description);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event remove {id}");
            }
        }

        [Fact]
        public async Task AddEvent_MissingDescription_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event add {FutureDate(10)}");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task AddEvent_InvalidDate_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event add 99/99 invalid-date-test");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("invalid date", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Uses "none" (not "notify") as the reserved tag under test - "event add notify ..." would instead
        /// route to the separate three-verb "event add notify" command (AddNotificationEvent), since Qmmands
        /// matches on the literal leading verbs before parameters are ever parsed.
        /// </summary>
        [Fact]
        public async Task AddEvent_ReservedTag_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event add none {FutureDate(10)} reserved-tag-test");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("reserved", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddNotificationEvent_WithoutTime_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event add notify {FutureDate(10)} notify-no-time-test");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("must have time", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddNotificationEvent_WithoutNotificationChannelConfigured_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event add notify {FutureDate(10)} 12:00 notify-no-channel-test");
            Assert.Contains("schedule set notifications", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddNotificationEvent_Succeeds()
        {
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}schedule set notifications <#{Fixture.ChannelId}>"));
            try
            {
                var addAck = await RunAdminCommandAsync($"{Options.CommandPrefix}event add notify {FutureDate(10)} 12:00 notify-test-{Guid.NewGuid():N}");
                ResponseAssert.IsSuccess(addAck);
                var id = ExtractEventId(addAck.Content);
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event remove {id}");
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}schedule reset notifications");
            }
        }

        [Fact]
        public async Task RemoveEvent_ById_Succeeds()
        {
            var addAck = await RunAdminCommandAsync($"{Options.CommandPrefix}event add {FutureDate(10)} remove-id-test-{Guid.NewGuid():N}");
            var id = ExtractEventId(addAck.Content);

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event remove {id}");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task RemoveEvent_NonexistentId_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event remove 999999999");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("find an event", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RemoveEvent_BySearch_Succeeds()
        {
            var description = "remove-search-" + Guid.NewGuid().ToString("N")[..8];
            await RunAdminCommandAsync($"{Options.CommandPrefix}event add {FutureDate(10)} {description}");

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event remove {description}");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task RemoveEvent_SearchNoMatches_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event remove nonexistent-search-{Guid.NewGuid():N}");
            Assert.Contains("no events found", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RemoveEvent_SearchMultipleMatches_ShowsChoices()
        {
            var shared = "shared-" + Guid.NewGuid().ToString("N")[..8];
            var addAck1 = await RunAdminCommandAsync($"{Options.CommandPrefix}event add {FutureDate(10)} {shared} one");
            var id1 = ExtractEventId(addAck1.Content);
            var addAck2 = await RunAdminCommandAsync($"{Options.CommandPrefix}event add {FutureDate(11)} {shared} two");
            var id2 = ExtractEventId(addAck2.Content);
            try
            {
                var result = await RunAdminCommandAsync($"{Options.CommandPrefix}event remove {shared}");
                ResponseAssert.Contains(result, "pick one event");
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event remove {id1}");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event remove {id2}");
            }
        }

        [Fact]
        public async Task EditEvent_Succeeds()
        {
            var addAck = await RunAdminCommandAsync($"{Options.CommandPrefix}event add {FutureDate(10)} edit-test-{Guid.NewGuid():N}");
            var id = ExtractEventId(addAck.Content);
            try
            {
                var newDescription = "edited-" + Guid.NewGuid().ToString("N")[..8];
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event edit {id} {newDescription}");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains(newDescription, ack.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event remove {id}");
            }
        }

        [Fact]
        public async Task EditEvent_NonexistentId_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event edit 999999999 new-description");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("find an event", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task EditEvent_NotifyWithoutTime_Fails()
        {
            var addAck = await RunAdminCommandAsync($"{Options.CommandPrefix}event add {FutureDate(10)} edit-notify-test-{Guid.NewGuid():N}");
            var id = ExtractEventId(addAck.Content);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event edit {id} notify");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("must have time", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event remove {id}");
            }
        }

        [Fact]
        public async Task TagEvent_Succeeds_ThenReservedTagFails()
        {
            var addAck = await RunAdminCommandAsync($"{Options.CommandPrefix}event add {FutureDate(10)} tag-test-{Guid.NewGuid():N}");
            var id = ExtractEventId(addAck.Content);
            try
            {
                var tag = "tag" + Guid.NewGuid().ToString("N")[..8];
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event tag {id} {tag}");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains(tag, ack.Content);

                var reservedAck = await RunAdminCommandAsync($"{Options.CommandPrefix}event tag {id} notify");
                ResponseAssert.IsFailure(reservedAck);
                Assert.Contains("reserved", reservedAck.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event remove {id}");
            }
        }

        [Fact]
        public async Task BatchEvent_Succeeds()
        {
            var description = "batch-test-" + Guid.NewGuid().ToString("N")[..8];
            var before = DateTimeOffset.UtcNow;
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event batch\nadd {FutureDate(10)} {description}");
            var ack = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
            ResponseAssert.IsSuccess(ack);
            Assert.Contains("batch finished", ack.Content, StringComparison.OrdinalIgnoreCase);

            var id = ExtractEventId(ack.Content);
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}event remove {id}");
        }

        [Fact]
        public async Task SearchEvent_InvokedByAnyone_NoMatches_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}event search nonexistent-{Guid.NewGuid():N}");
            ResponseAssert.IsFailure(ack);
        }

        [Fact]
        public async Task ListEvents_InvokedByAnyone_Succeeds()
        {
            var futureMonth = DateTimeOffset.UtcNow.AddMonths(2);
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}event list {futureMonth:MMMM} {futureMonth:yyyy}");
            Assert.Contains("no events have been added", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ListEvents_InvalidMonth_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}event list NotAMonth");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("unrecognizable month", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateCalendar_Succeeds_AndPostsRealEmbed()
        {
            await using var channel = await TemporaryEntity.CreateChannelAsync(Fixture.Admin, Options.GuildId, "cal-" + Guid.NewGuid().ToString("N")[..8]);
            var before = DateTimeOffset.UtcNow;
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar create <#{channel.Id}> {FutureDate(1)} {FutureDate(30)}");
            ResponseAssert.IsSuccess(ack);

            var posted = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, channel.Id, before, AckTimeout);
            Assert.Single(posted.Embeds);

            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}calendar delete {posted.Id}");
        }

        [Fact]
        public async Task CreateCalendar_InvalidDateRange_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar create <#{Fixture.ChannelId}> {FutureDate(30)} {FutureDate(1)}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("earlier than the end date", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateMonthCalendar_Succeeds()
        {
            var futureMonth = DateTimeOffset.UtcNow.AddMonths(2);
            var (ack, id) = await CreateCalendarInRunChannelAsync($"{Options.CommandPrefix}calendar create month <#{Fixture.ChannelId}> {futureMonth:MMMM} {futureMonth:yyyy}");
            ResponseAssert.IsSuccess(ack);
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}calendar delete {id}");
        }

        [Fact]
        public async Task CreateUpcomingSpanCalendar_Succeeds()
        {
            var (ack, id) = await CreateCalendarInRunChannelAsync($"{Options.CommandPrefix}calendar create upcoming <#{Fixture.ChannelId}> 14");
            ResponseAssert.IsSuccess(ack);
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}calendar delete {id}");
        }

        [Fact]
        public async Task CreateUpcomingWeekCalendar_Succeeds()
        {
            var (ack, id) = await CreateCalendarInRunChannelAsync($"{Options.CommandPrefix}calendar create upcoming week <#{Fixture.ChannelId}>");
            ResponseAssert.IsSuccess(ack);
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}calendar delete {id}");
        }

        [Fact]
        public async Task SetCalendar_UpdatesBeginEndTitleFooterTag_Succeeds()
        {
            var (createAck, id) = await CreateCalendarInRunChannelAsync($"{Options.CommandPrefix}calendar create <#{Fixture.ChannelId}> {FutureDate(1)} {FutureDate(30)}");
            ResponseAssert.IsSuccess(createAck);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}calendar set begin {id} {FutureDate(2)}"));
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}calendar set end {id} {FutureDate(40)}"));

                var title = "title-" + Guid.NewGuid().ToString("N")[..8];
                var titleAck = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar set title {id} {title}");
                ResponseAssert.IsSuccess(titleAck);

                var footer = "footer-" + Guid.NewGuid().ToString("N")[..8];
                var footerAck = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar set footer {id} {footer}");
                ResponseAssert.IsSuccess(footerAck);

                var tag = "caltag" + Guid.NewGuid().ToString("N")[..8];
                var tagAck = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar set tag {id} {tag}");
                ResponseAssert.IsSuccess(tagAck);
                Assert.Contains(tag, tagAck.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}calendar delete {id}");
            }
        }

        [Fact]
        public async Task SetCalendarTitle_NonexistentId_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar set title 999999999 title");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("can't find a calendar", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ListCalendars_Empty_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar list");
            Assert.Contains("no calendars found", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ListCalendars_WithCalendars_ListsThem()
        {
            var (createAck, id) = await CreateCalendarInRunChannelAsync($"{Options.CommandPrefix}calendar create <#{Fixture.ChannelId}> {FutureDate(1)} {FutureDate(30)}");
            ResponseAssert.IsSuccess(createAck);
            try
            {
                var result = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar list");
                ResponseAssert.Contains(result, id.ToString());
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}calendar delete {id}");
            }
        }

        [Fact]
        public async Task SplitCalendar_Succeeds()
        {
            var (createAck, originalId) = await CreateCalendarInRunChannelAsync($"{Options.CommandPrefix}calendar create <#{Fixture.ChannelId}> {FutureDate(1)} {FutureDate(30)}");
            ResponseAssert.IsSuccess(createAck);
            try
            {
                var (splitAck, newId) = await CreateCalendarInRunChannelAsync($"{Options.CommandPrefix}calendar split {originalId} {FutureDate(15)}");
                ResponseAssert.IsSuccess(splitAck);
                try
                {
                    var listResult = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar list");
                    Assert.Contains($"Id: `{originalId}`", listResult.Content);
                    Assert.Contains($"Id: `{newId}`", listResult.Content);
                }
                finally
                {
                    await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}calendar delete {newId}");
                }
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}calendar delete {originalId}");
            }
        }

        [Fact]
        public async Task SplitCalendar_DateOutOfRange_Fails()
        {
            var (createAck, id) = await CreateCalendarInRunChannelAsync($"{Options.CommandPrefix}calendar create <#{Fixture.ChannelId}> {FutureDate(1)} {FutureDate(30)}");
            ResponseAssert.IsSuccess(createAck);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar split {id} {FutureDate(100)}");
                ResponseAssert.IsFailure(ack);
                // Source has a typo ("betweeen") - check a substring that doesn't span it instead.
                Assert.Contains("date has to be", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}calendar delete {id}");
            }
        }

        [Fact]
        public async Task SwapCalendars_Succeeds()
        {
            var (createAck1, id1) = await CreateCalendarInRunChannelAsync($"{Options.CommandPrefix}calendar create <#{Fixture.ChannelId}> {FutureDate(1)} {FutureDate(10)}");
            ResponseAssert.IsSuccess(createAck1);
            var (createAck2, id2) = await CreateCalendarInRunChannelAsync($"{Options.CommandPrefix}calendar create <#{Fixture.ChannelId}> {FutureDate(11)} {FutureDate(20)}");
            ResponseAssert.IsSuccess(createAck2);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar swap {id1} {id2}");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains("swapped", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}calendar delete {id1}");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}calendar delete {id2}");
            }
        }

        [Fact]
        public async Task DeleteCalendar_Succeeds_AndDeletesRealMessage()
        {
            var (createAck, id) = await CreateCalendarInRunChannelAsync($"{Options.CommandPrefix}calendar create <#{Fixture.ChannelId}> {FutureDate(1)} {FutureDate(30)}");
            ResponseAssert.IsSuccess(createAck);

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar delete {id}");
            ResponseAssert.IsSuccess(ack);

            Assert.Null(await TryFetchMessageAsync(Fixture.ChannelId, id));
        }

        [Fact]
        public async Task DeleteCalendar_NonexistentId_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}calendar delete 999999999");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("cannot find a calendar", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        private static int ExtractEventId(string content)
        {
            var match = System.Text.RegularExpressions.Regex.Match(content, @"ID `(\d+)`");
            Assert.True(match.Success, $"Could not find an event ID in: \"{content}\"");
            return int.Parse(match.Groups[1].Value);
        }

        /// <summary>
        /// Calendar-creating commands post a new embed to the target channel before their textual ack - two
        /// messages, not one, when the target is the run channel. Waits for the embed first, then the ack; the
        /// embed's own message ID is the calendar's ID (the ack text never includes it).
        /// </summary>
        private async Task<(IGatewayUserMessage Ack, Snowflake CalendarId)> CreateCalendarInRunChannelAsync(string commandText)
        {
            var before = DateTimeOffset.UtcNow;
            await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, commandText);
            var posted = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, before, AckTimeout);
            var ack = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, posted.Id.CreatedAt.AddMilliseconds(1), AckTimeout);
            return (ack, posted.Id);
        }

        private async Task<IMessage?> TryFetchMessageAsync(Snowflake channelId, Snowflake messageId)
        {
            try
            {
                return await Fixture.Admin.Client.FetchMessageAsync(channelId, messageId);
            }
            catch (RestApiException ex) when (ex.StatusCode == HttpResponseStatusCode.NotFound)
            {
                return null;
            }
        }

        private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
            Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);

        private Task<IGatewayUserMessage> RunLowPrivCommandAsync(string commandText) =>
            Fixture.LowPriv.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);
    }
}
