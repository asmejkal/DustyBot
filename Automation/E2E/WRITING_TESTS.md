# Writing integration tests for a new module

Context for extending this suite to another module, distilled from how `GreetByeModuleTests`,
`YouTubeModuleTests`, `ReactionsModuleTests`, `NotificationsModuleTests`, `LogModuleTests`, `InfoModuleTests`,
`DaumCafeModuleTests`, `BotModuleTests`, `AdministrationModuleTests`, `AutorolesModuleTests`,
`LastFmModuleTests`, `PollModuleTests`, `RaidProtectionModuleTests`, `RolesModuleTests`, `ScheduleModuleTests`,
`StarboardModuleTests`, `TranslatorModuleTests`, and `SpotifyModuleTests` were built (`ThreadBehaviorTests`
covers cross-cutting thread behavior that isn't tied to one module). Read the target module's source
(`Modules/<X>Module.cs` and its service) first - the points below are the *process* and *pitfalls*, not a
substitute for understanding the actual commands.

- **Modules not yet migrated to `DustyBot.Service.New`** (still living in `DustyBot.Service`, the old
  Discord.Net-based stack) can still be covered here ahead of migration, to give the migration something to
  verify against. Their `Success`/`Failure` acks (`Communicator.CommandReplySuccess`/`CommandReplyError`) use the
  same markers as `DustyModuleBase` but are never reply-linked - correlate by timestamp
  (`WaitForAnyMessageAsync`) instead of `WaitForReplyAsync` so the same test works unchanged before and after
  migration (see `AdministrationModuleTests`).
- **An old-stack module with its own raw gateway message listener won't see the tester bots at all by
  default**, the same way New-stack services needed a whitelist before this suite could test their real
  auto-trigger behavior. `RaidProtectionModule.HandleMessageReceived` unconditionally ignored bot authors; a
  parallel `BotIntegrationOptions`/`AllowedInteractionBotIds` was added to the old stack
  (`Service/src/DustyBot.Service/Configuration/BotIntegrationOptions.cs`) mirroring the New-stack one, bound the
  old-stack way (flat env var, no section prefix - `Configure<BotIntegrationOptions>(config)` against root
  config, not a named section). Confirm with the user before patching old-stack production code like this -
  it's a real, if small, behavior change, not just test code. `RolesModule.HandleMessageReceived` had the exact
  same shape of gap and reuses the same options class - once one old-stack module has the whitelist wired up,
  check every other module with its own `MessageReceived`/gateway-event handler for the same `if (...IsBot)
  return;` pattern rather than treating each discovery as a one-off.
- **The whitelist check doesn't always gate on the same user.** `RaidProtectionModule`/`RolesModule` gate on the
  *acting* user (the message author whose command/message triggers processing). `StarboardModule` instead
  unconditionally skipped reposting any message *authored* by a bot (`ProcessNewStar`/`ProcessRemovedStar`
  checking `message.Author.IsBot`), regardless of who reacted - so a tester bot's own messages could never be
  starred for real even with the acting-user whitelist already in place elsewhere. Added a small
  `IsBlockedBotAuthor(IUser author)` helper reusing the same `BotIntegrationOptions.AllowedInteractionBotIds`,
  but checked against the *message author*, not the reactor. Confirm which party a module's bot-check actually
  gates on before assuming the acting-user pattern applies unchanged.
- **A module already having a same-named class under `DustyBot.Service.New` doesn't mean it's actually live
  there.** `TranslatorModule` exists in both stacks, but the New-stack one's only dependency
  (`ITranslatorService`) is never registered in that project's `ServiceCollectionExtensions` - every other
  migrated module's service has an explicit `AddXServices()` registration to compare against. A module class
  existing isn't proof of a finished migration; check its dependencies actually resolve (or just check for the
  registration call) before assuming the old-stack version is stale or redundant to test.

## Harness pieces to reuse (don't rebuild these)

- **`IntegrationTestFixture`** (`Harness/IntegrationTestFixture.cs`) - one shared fixture for the whole run:
  connects the `Admin` and `LowPriv` tester bots once, creates `ChannelId`/`NoSendChannelId`/`NoEmbedChannelId`
  for the run. Every test class shares it via `[Collection(DiscordCollection.Name)]` and a constructor parameter.
- **`TestDiscordClient`** (`Harness/TestDiscordClient.cs`) - wraps one tester bot: `SendCommandAsync`,
  `SendMessageAsync`, `SendCommandWithAttachmentAsync`, `SendEmbedAsync`; `WaitForReplyAsync` (reply-linked acks),
  `WaitForFollowUpMessageAsync` (non-reply follow-up after a timestamp), `WaitForAnyMessageAsync` (either, by
  timestamp only). Sends are self-paced (>=1.6s apart) to stay under the framework's default rate limit.
  `RunAndWaitForReplyAsync`/`RunAndWaitForAnyMessageAsync`/`RunAndWaitForFollowUpAsync` each combine a
  `SendCommandAsync` with the matching `WaitForX` call in one step - **use these instead of hand-rolling a
  per-file `RunAdminCommandAsync`/`RunLowPrivCommandAsync` helper**. Every module test file used to declare its
  own copy of this exact send-then-wait dance (differing only in which `WaitForX` it called); that duplication
  was consolidated into the harness once it had been copy-pasted into ~18 files. A module test class should
  still keep its own thin, module-scoped wrapper for readability at call sites, but that wrapper should be a
  one-line delegation, e.g.:
  ```csharp
  private Task<IGatewayUserMessage> RunAdminCommandAsync(string commandText) =>
      Fixture.Admin.RunAndWaitForAnyMessageAsync(Options.TargetBotId, Fixture.ChannelId, commandText, AckTimeout);
  ```
  not a reimplementation of the send/wait logic itself. Genuinely bespoke correlation logic (e.g.
  `AutorolesModuleTests.RunAdminLongRunningCommandAsync`, which loops past an intermediate progress message) is
  the exception - don't force something like that through these three shapes.
- **`ResponseAssert`** (`Harness/ResponseAssert.cs`) - `IsSuccess`/`IsFailure` for marker-based acks
  (`DustyModuleBase.Success`/`Failure`, prefixed with ✅/⛔); `Contains` for neutral `Result(...)`/paged
  `Listing`/`Table` responses, which carry no marker and may be plain text or an embed.
- **`TestAnnouncer`** (`Harness/TestAnnouncer.cs`) - posts a `▶ ClassName.MethodName` embed before each test, via
  a reflection trick against `ITestOutputHelper` (xUnit doesn't expose the running test's name officially). Call
  it from every test class's `InitializeAsync`.
- **`RunOnceGate`** (`Harness/RunOnceGate.cs`) - runs an async action exactly once, however many times it's
  called. Use for a module's one-time reset of *shared, non-per-test-scoped* state (see below) - only add this
  if a specific test actually needs it, not by default.
- **`TemporaryEntity`** (`Harness/TemporaryEntity.cs`) - creates a channel/role and deletes it again via
  `await using`. Covers the "scratch entity for one test" pattern (a log target channel, a manager role) - if a
  test needs extra cleanup beyond deleting the entity itself (e.g. revoking a role grant), keep that part in its
  own `try`/`finally` alongside the `await using`. Also exposes `.Name` (the name it was created with) for
  modules that key off role/channel *names* rather than IDs, like `RolesModule`'s message-based self-assignment.
  Threads are deliberately not covered here - see below.

## Conventions

- **Isolate with unique names, not cleanup.** Generate a GUID-based trigger/category/song name per test
  (`"trig" + Guid.NewGuid().ToString("N")[..8]`) instead of relying on a clean starting state. This makes tests
  robust regardless of run history and lets them run in any order. Still remove your own entry in `DisposeAsync`
  (best-effort, unasserted) for tidiness.
- **Only add a `RunOnceGate` reset when a test genuinely needs a clean baseline** - e.g. a module-wide singleton
  setting (Reactions' manager role), or a shared bucket a paginated view reads from (YouTube's "default"
  category, where leftover entries could push a fresh item past page 1). Don't add it reflexively - GreetBye
  needed none (each test already calls its own per-test `disable`), and most of YouTube/Reactions' state is
  already collision-proof via unique naming.
- **Correlate responses correctly**: `WaitForReplyAsync` for `Success`/`Failure` acks (they're reply-linked);
  `WaitForAnyMessageAsync`/`WaitForFollowUpMessageAsync` for paged menus (`Listing`/`Table`/`NumberedListing`)
  or bare `Success()`/no-ack commands whose only observable effect is a separate message - these aren't
  reliably reply-linked.
- **Map out every command's actual permission attribute before writing tests** - don't assume one tier per
  module. Attributes seen so far: `RequireAuthorAdministrator`, `RequireAuthorContentManager`, and
  module-specific nested ones (e.g. `ReactionsModule.RequireAuthorReactionsManager`, checking ManageMessages OR
  a configurable role). Test both an Admin-succeeds and a LowPriv-fails case per distinct tier, using the
  attribute's own failure text loosely (`Assert.Contains("Manage Messages", ack.Content)`).
- **Watch for `[RateLimit(..., RateLimitBucketType.Guild)]`** on top of the framework's default per-user limit -
  it's usually per-command (safe to call once each), but never invoke the *same* guild-rate-limited command
  twice within its window in one run (e.g. export/import at 1/min each), unless testing the rate limit itself.
  The old framework has hand-rolled equivalents too, sometimes with surprising windows - `AutorolesModule.apply`/
  `check` lock the whole guild out for 1 hour / 30 minutes *after the command finishes* (not just while it's
  running), so each is invoked for real exactly once per suite run, combined into a single test that also checks
  the resulting lockout message right after (see `AutorolesModuleTests`).
- **A shared list-like setting with no bulk-clear command** (e.g. `AutorolesModule`'s configured auto-roles) can
  still get a clean-baseline `RunOnceGate` reset: query the list command once, regex out the IDs from its
  content, and remove each individually. Cheap to write and only runs once per suite, same as any other
  `RunOnceGate` reset.
- **Bot-authored messages are ignored by default** wherever a module reads raw gateway messages outside the
  command pipeline (`DustyBotSharder.OnMessage` for commands, `ReactionsService.OnMessageReceived` for
  auto-triggers - there may be others). If a module has this kind of check and you want to test the real
  behavior (not just the command surface), whitelist the tester bots via the existing
  `BotIntegrationOptions.AllowedInteractionBotIds` check rather than inventing a new mechanism.
- **Don't guess Disqord's alpha-package API.** Either check the source on GitHub, reflect against the installed assembly to confirm a
  method signature before using it, or write the plausible call and let the compiler catch a mismatch - both
  were used this session; the latter is fine once you've seen the pattern work elsewhere (e.g.
  `CreateTextChannelAsync`/`DeleteChannelAsync` confirmed the `Create*Async`/`Delete*Async(guildId, ...)` shape,
  which `CreateRoleAsync`/`DeleteRoleAsync`/`GrantRoleAsync`/`RevokeRoleAsync` then followed correctly on the
  first try).
- **Retry on failure, don't blind-sleep.** When an action depends on gateway cache propagation (e.g. granting a
  role, then expecting a permission check to see it), retry the dependent call a few times - each
  `SendCommandAsync` call is already paced >=1.6s apart, which is normally enough - rather than adding a fixed
  `Task.Delay` up front.
- **Flag production bugs you find** (report before fixing, unless told otherwise) - this suite has already
  caught two real ones: a missing `[RequireAuthorAdministrator]` on `bye embed set footer`, and
  `ReactionsService.AddReactionAsync` incrementing its ID counter twice so the reported ID didn't match the
  actual stored reaction.
- **`[HideInvocation]` commands never reply-link their ack** - `DustyModuleBase.ShouldReply` checks for the
  attribute directly and is unconditionally false when it's present, regardless of whether the bot could
  actually delete the invocation. Use `WaitForFollowUpMessageAsync` (timestamp-based), not `WaitForReplyAsync`,
  for these. The same applies to any command invoked outside a guild context (a DM), since `ShouldReply` also
  requires a guild context.
- **Discord blocks bot-to-bot DMs entirely, in both directions.** If a module's only observable effect is a
  direct message and both tester accounts are bots, you cannot verify delivery by waiting for the DM - it will
  never arrive, and asserting "no DM arrived" as a negative case is meaningless since that's also just the
  permanent, unconditional outcome. Verify via whatever the command's ack does expose instead - e.g.
  `NotificationsModule.list` explicitly acks with a `Failure` when it can't deliver its DM
  (`RestApiException(CannotSendMessagesToThisUser)`), which is a deterministic, checkable substitute for
  actually seeing the DM.
- **The old framework has a third ack marker beyond Success/Failure**: `Communicator.QuestionMarker` (❔), sent
  via `UnclearParametersCommandException` for "I'm not sure what you meant, did you mean X?"-style responses
  (e.g. `PollModule.Vote`'s ambiguous/unrecognized answer text). `ResponseAssert` only knows the two markers
  DustyModuleBase's New-framework `Success`/`Failure` use - check these by content substring instead of adding a
  third marker method for what so far is a single old-stack case.
- **A built-in Disqord check's failure response isn't guaranteed to look like DustyBot's own.** Custom attributes
  (`RequireAuthorAdministrator`, etc.) always ack through `Success`/`Failure`, so the marker and reply-linking are
  reliable. Built-in ones (`RequireGuild`, `RequireBotOwner`) go through Disqord's own default handling instead,
  which may not carry the ⛔ marker or be reply-linked at all - assert on the observable *effect* instead of
  guessing the response's shape (e.g. `BotModuleTests.HelpDump_InvokedByNonOwner_Fails` accepts either "some
  response with no attachment" or "no response at all", since either is consistent with rejection).
- **Some services validate input locally before ever making a network call.** `DaumCafeSession.GetCafeAndBoardId`
  checks the URL against a regex before touching the network, so `cafe add` with an unrecognized link can be
  tested without hitting Daum's servers. When a command's `[Example]` includes a real, stable resource, hitting
  the real service for the success path is fine too (`DaumCafeModuleTests.Add_WithRealBoardLink_...`,
  `YouTubeModuleTests`' well-known video) - but only for services stable and public enough not to make the suite
  flaky (skip this for anything that could be country-blocked, rate-limited, or require a login).
- **Threads**: `TestDiscordClient.Client` exposes `CreatePublicThreadAsync`/`CreatePrivateThreadAsync(channelId,
  name)` and `FetchThreadMembersAsync(threadId)` (all on `IRestClient`, from `Disqord.Rest`). A public thread
  behaves like a regular channel for most purposes (visible to the bot without being a member, so e.g.
  `LogService` still logs deletions in it); a private thread only exposes messages to the bot if it was
  explicitly added (`IThreadChannel.CurrentMember` is null otherwise). `IMessageGuildChannel`-typed parameters
  (channel arguments) accept a thread mention the same as a channel mention, since threads implement that
  interface too. Call these two methods directly rather than through `TemporaryEntity` - like the run's main
  channel, threads are left up (not deleted) so they can be reviewed in Discord afterward.
- **A command family with both a 2-verb and a 3-verb registration sharing a prefix can silently swallow a
  parameter value that happens to equal the 3-verb's own extra verb.** `ScheduleModule` registers both
  `event add <Tag?> <Date> ...` and `event add notify <Tag?> <Date> ...` (a completely different method,
  `AddNotificationEvent`) - Qmmands matches on the literal leading verb tokens before any parameter parsing
  happens, so `event add notify <date> <description>` (intending "notify" as the *value* of the optional Tag
  parameter) actually invokes the wrong command entirely, with Tag left unset. Pick a reserved/test value that
  isn't also a sibling command's verb (e.g. use `none` instead of `notify` to test `ScheduleModule`'s
  reserved-tag rejection).
- **A command that posts a new message and then acks separately sends two new messages to the same channel
  when the target and invoking channel are the same** - `ScheduleModule`'s `calendar create*`/`calendar split`
  post the calendar's embed *before* the textual `ReplySuccess`, exactly like `PollModule.StartPoll` posts the
  poll embed before (sometimes) acking. A single `WaitForAnyMessageAsync` call only catches the first of the
  two (the embed, which carries no marker and won't look like a success ack) - wait for it explicitly, then
  advance the cursor to its timestamp before waiting again for the actual ack, the same "collect messages
  sequentially" approach used for `RaidProtectionModuleTests`' warn+log pair. This is also usually the only way
  to get an entity's real ID when the ack text doesn't include it - the posted message's own ID often *is* the
  ID (e.g. a calendar's `MessageId` field is exactly the embed message Discord assigned it).
- **When a per-user real-data command needs an OAuth-connected third-party account that neither tester bot can
  ever hold itself** (unlike LastFmModule's public-by-username profiles, SpotifyModule's per-user data is only
  readable through an account its owner authorized via a real browser OAuth flow - no tester bot token can drive
  that), prompt a human interactively instead of leaving it as a permanent manual gap: post the connect link
  (read directly off a live response rather than hardcoded, so nothing here needs to know the website's URL) and
  instructions to the run channel, then use `TestDiscordClient.WaitForMessageAsync` with a custom predicate
  (`!m.Author.IsBot`) to wait for any human reply - that reply's author becomes the real-account target for the
  rest of the suite run. Gate the one-time prompt behind a `RunOnceGate`; since the gate only latches after the
  action completes without throwing, a timed-out prompt naturally retries on the next test that needs it rather
  than caching a failure. See `SpotifyModuleTests.EnsureRealAccountUserIdAsync`.
- **A `ParameterType.Regex` parameter marked `Optional` doesn't hard-fail on a non-matching token - it's
  silently skipped**, per `CommandParser.ParseParameters`: an Optional parameter whose format check fails just
  falls through to the next parameter instead of returning `InvalidParameterFormat`. `SpotifyModule.Stats`
  declares `Period` this way, so passing garbage where `Period` goes doesn't reach `ParseStatsPeriod`'s own
  "Invalid time period." exception at all - it falls through to the next (also Optional) `User` parameter, fails
  that too, and ends up unconsumed, producing "too many parameters" instead. Before asserting a specific
  in-command validation message for an Optional regex/typed parameter, trace whether the token could fall
  through to a later parameter and produce a generic parser-level rejection instead.
