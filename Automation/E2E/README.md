# DustyBot.Automation.E2E

Black-box integration tests that drive a live DustyBot instance (`DustyBot.Service.New`) through two second
Discord bot accounts ("tester bots") in a dedicated test guild, and loosely validate its responses. This is a
manually-run local/dev suite - it needs live secrets and a persistent Discord guild, so it is **not** wired into
CI (`.github/workflows/dotnet.yml`).

> **Heads up:** some tests need user interaction. Four tests make the low-privilege tester bot leave the guild
> (once via a real leave, once via ban+unban, twice more for rejoin-triggered role tests) and post a re-invite
> link each time - expect to click four links for a full run. `SpotifyModuleTests`' first `RealAccount_*` test
> posts a Spotify connect link instead: open it, log in with any Discord account in the test guild, connect a
> Spotify account with real listening history, then reply in the channel - that account becomes the target for
> the rest of the run. Only that one test fails if nothing replies in time (`IntegrationTests__SpotifyConnectTimeoutSeconds`,
> default 10 minutes); re-running retries the prompt.

## One-time test guild setup

1. Create a scratch Discord server (or reuse an existing dev/test one) and create two bot applications in the
   [Discord Developer Portal](https://discord.com/developers/applications) if you don't already have spares:
   an **admin tester bot** and a **low-privilege tester bot**. Enable the "Message Content Intent" for both,
   and for the target bot under test.
2. Invite all three bots to the test guild:
   - The target DustyBot instance (the one you're testing) - grant its role **Send Messages**, **Manage
     Messages**, and **Embed Links** server-wide (via a role, not a per-channel overwrite). Every test run
     creates fresh channels, so a permission set up on one run's channel wouldn't carry over to the next.
   - The admin tester bot - grant it the **Administrator** permission (via a role).
   - The low-privilege tester bot - no elevated permissions; it's used to verify that admin-gated commands
     correctly reject non-admins.

## Running the target bot locally

The target bot needs `BotIntegration__AllowedInteractionBotIdsList` set to both tester bots' user IDs
(comma-separated) so it accepts their commands - see `DustyBotSharder.OnMessage` and `BotIntegrationOptions`.
A minimal local run only needs Mongo (Elasticsearch is optional - leave `Logging__ElasticsearchNodeUri` unset):

```
docker-compose up dustybot-mongodb
```

Then run `DustyBot.Service.New` (e.g. from your IDE, or `dotnet run --project Service/src/DustyBot.Service.New`)
with at least:

```
Discord__Token=<target bot token>
Bot__DefaultCommandPrefix=<prefix, e.g. !>
Bot__OwnerID=<your user id>
Mongo__ConnectionString=mongodb://localhost:27017/dustybot
BotIntegration__AllowedInteractionBotIds__0=<admin tester bot id>
BotIntegration__AllowedInteractionBotIds__1=<low-priv tester bot id>
```

## Running the tests

Set the following environment variables, then run `dotnet test` against this project:

| Variable | Value |
|---|---|
| `IntegrationTests__AdminTesterBotToken` | Admin tester bot's token |
| `IntegrationTests__LowPrivTesterBotToken` | Low-privilege tester bot's token |
| `IntegrationTests__TargetBotId` | Target bot's user ID |
| `IntegrationTests__GuildId` | Test guild ID |
| `IntegrationTests__CommandPrefix` | Same prefix configured on the target bot (`Bot__DefaultCommandPrefix`) |
| `IntegrationTests__RejoinTimeoutSeconds` | *(optional)* How long to wait for the re-invite to be clicked; defaults to 600 (10 minutes) |
| `IntegrationTests__SpotifyConnectTimeoutSeconds` | *(optional)* How long to wait for a person to connect a real Spotify account and reply during `SpotifyModuleTests`; defaults to 600 (10 minutes) |

**In Visual Studio:** `dotnet test`'s environment variables don't reach Test Explorer's own test host process.
Instead, copy `IntegrationTests.runsettings.example` to `IntegrationTests.runsettings` (gitignored) in this
folder, fill in the values, then Test > Configure Run Settings > Select Solution Wide runsettings File and pick
it.

```
dotnet test Automation/E2E
```

Tests run sequentially (see `DiscordCollection`) since they share guild/channel state and are subject to the
target bot's per-user rate limit (5 commands / 7.5s, see `DustyBotSharder.SetDefaultRateLimits`).

## Known gaps requiring manual testing

### NotificationsModule

Discord blocks bot-to-bot DMs entirely, so `NotificationsModuleTests` can only verify command acks, not real
delivery. Add a real personal account to the test guild to verify these manually:

- A keyword mention delivers a DM with the correct trigger word, guild name, and jump link.
- `pause`/`resume`, `ignore channel`, `block`/`unblock`, and `opt out` actually gate delivery as documented.
- `ignore active channel` delays/suppresses delivery within the activity window.
- `list` DMs an embed with correct keywords and trigger counts.
- `block`/`unblock` work via DM to the bot; a guild-only command is rejected via DM.
- A keyword mention inside a private thread never triggers a notification.

### DaumCafeModule

`Add_WithRealBoardLink_SucceedsWithoutPreviews` covers a real round trip to cafe.daum.net. Not exercised:
`cafe add` against an inaccessible board (`InaccessibleBoard`), the 26th-feed `TooManyFeeds` rejection, and a
running feed actually posting new content on an update tick.

### BotModule

- `help dump` succeeding for the real bot owner - only the non-owner rejection is covered here, since neither
  tester bot can be `Bot__OwnerID`. Run manually as the owner and check the `output.html` attachment.

### LogModule

- Discord's bulk message delete (`LogService.OnMessagesDeleted`) isn't exercised, only single-message deletion.

### AdministrationModule

- `ban`'s real effect is only covered for a single user; banning multiple users at once and role-hierarchy
  rejection aren't exercised.
- `autoban`'s regex-complexity rejection isn't exercised - reliably triggering it needs a tuned
  catastrophic-backtracking regex, too fragile to encode as a test.
- `moddm` actually delivering its DM isn't covered (needs 100+ guild members and a real non-bot recipient).

### AutorolesModule

- `autorole add` rejecting a role at/above the bot's highest role isn't exercised - would need reordering roles
  via REST, risking the test guild's hierarchy.
- `autorole apply`/`check` self-lock the guild for 1h/30min after completing, so each is only invoked for real
  once per suite run; re-running within that window sees the lockout message. Worth a second look during
  migration - the lockout starts when the command *finishes*, not starts (`// TODO: implement command rate
  limits properly` in the source).

### LastFmModule

- The "Last.fm is down"/403-Forbidden `WebException` branches aren't exercised (need a real outage or a
  privacy-setting change on the account in use).
- DM-invokable commands (`lf set`/`set anonymous`/`reset`) aren't covered - same bot-to-bot DM limitation as
  NotificationsModule.

### RaidProtectionModule

- `MassMentionsRule`'s real trigger isn't covered - it needs 10 real human mentions, and the test guild doesn't
  have that many members.
- `ImageSpamRule`'s real trigger isn't covered - it's disabled by default with no non-owner command to enable
  it (only the owner-only `rules set`/`rules reset`, covered here just for their rejection path).

### RolesModule

- A role at/above the bot's highest role being rejected by `roles add`/`create`/`set secondary` isn't exercised.

### ScheduleModule

- `schedule set notifications`'s non-mentionable-role rejection isn't exercised - needs an intermediate-privilege
  account (ManageMessages but not Administrator).
- `RefreshResult`'s `MessageTooLong`/`MissingPermissions` partial-failure reasons aren't exercised.
- `event batch`'s error-reporting path isn't exercised, only the clean-success path.
- Calendar's month-specific footer text (shown only for a month calendar with a custom title) isn't exercised.

### SpotifyModule

- The "Spotify API is currently unavailable" (HTTP 503) branch isn't exercised.
- `sf reset` disconnecting an actually-connected account isn't exercised - only the "never connected" no-op
  path is, since neither tester bot can hold a real connected account itself.
- Only one real account is ever connected per run, always targeted as "other user" - the "self" lookup path
  isn't exercised for real.
