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
    /// Command matrix for RolesModule ("roles" group, old stack - not yet migrated). Acks are correlated by
    /// timestamp, not reply, since the old framework never reply-links.
    /// </summary>
    [Collection(DiscordCollection.Name)]
    public sealed class RolesModuleTests : IAsyncLifetime
    {
        private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RealEffectTimeout = TimeSpan.FromSeconds(20);
        private static readonly RunOnceGate SetupOnce = new();
        private static Snowflake _roleChannelId;

        private readonly ITestOutputHelper _output;

        public RolesModuleTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            _output = output;
        }

        private IntegrationTestFixture Fixture { get; }

        private IntegrationTestOptions Options => Fixture.Options;

        public async Task InitializeAsync()
        {
            await TestAnnouncer.AnnounceAsync(Fixture.Admin, Fixture.ChannelId, _output);
            await SetupOnce.RunOnceAsync(async () =>
            {
                var channel = await Fixture.Admin.Client.CreateTextChannelAsync(Options.GuildId, "roles-channel-" + Guid.NewGuid().ToString("N")[..8]);
                _roleChannelId = channel.Id;
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles set channel <#{_roleChannelId}>");
            });
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SetRoleChannel_Succeeds()
        {
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles set channel <#{_roleChannelId}>");
                ResponseAssert.IsSuccess(ack);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles set channel <#{_roleChannelId}>");
            }
        }

        [Fact]
        public async Task SetRoleChannel_TargetingChannelWithoutSendPermission_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles set channel <#{Fixture.NoSendChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("can't send messages", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetRoleChannel_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles set channel <#{_roleChannelId}>");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// SetRoleChannel only rejects a target missing ManageMessages if clearing is *already* enabled - so
        /// this enables it first (against the normal role channel, which has full permissions) before pointing
        /// at a channel where the bot's ManageMessages is explicitly denied.
        /// </summary>
        [Fact]
        public async Task SetRoleChannel_ClearingEnabledButTargetLacksManageMessages_Fails()
        {
            var restrictedChannel = await Fixture.Admin.Client.CreateTextChannelAsync(Options.GuildId, "roles-nomanage-" + Guid.NewGuid().ToString("N")[..8], x =>
                x.Overwrites = new[]
                {
                    new LocalOverwrite(Options.TargetBotId, OverwriteTargetType.Member, new OverwritePermissions(Permissions.None, Permissions.ManageMessages)),
                });
            await EnsureClearingAsync(enabled: true);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles set channel <#{restrictedChannel.Id}>");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("does not have the ManageMessages permission", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await EnsureClearingAsync(enabled: false);
                await Fixture.Admin.Client.DeleteChannelAsync(restrictedChannel.Id);
            }
        }

        [Fact]
        public async Task ToggleClearing_TogglesOnAndOff()
        {
            var onAck = await RunAdminCommandAsync($"{Options.CommandPrefix}roles toggle clearing");
            ResponseAssert.IsSuccess(onAck);
            try
            {
                Assert.Contains("enabled", onAck.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                var offAck = await RunAdminCommandAsync($"{Options.CommandPrefix}roles toggle clearing");
                ResponseAssert.IsSuccess(offAck);
                Assert.Contains("disabled", offAck.Content, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task ToggleClearing_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles toggle clearing");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Unlike SetRoleChannel's equivalent check, this one looks at the *current* role channel's permissions
        /// at the moment clearing is turned on - so the role channel is temporarily repointed at a
        /// permission-restricted channel (allowed, since clearing is off at that point) before toggling.
        /// </summary>
        [Fact]
        public async Task ToggleClearing_RoleChannelLacksManageMessages_Fails()
        {
            var restrictedChannel = await Fixture.Admin.Client.CreateTextChannelAsync(Options.GuildId, "roles-nomanage-" + Guid.NewGuid().ToString("N")[..8], x =>
                x.Overwrites = new[]
                {
                    new LocalOverwrite(Options.TargetBotId, OverwriteTargetType.Member, new OverwritePermissions(Permissions.None, Permissions.ManageMessages)),
                });
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles set channel <#{restrictedChannel.Id}>"));
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles toggle clearing");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("needs the ManageMessages permission", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles set channel <#{_roleChannelId}>");
                await Fixture.Admin.Client.DeleteChannelAsync(restrictedChannel.Id);
            }
        }

        [Fact]
        public async Task TogglePersistence_TogglesOnAndOff()
        {
            var onAck = await RunAdminCommandAsync($"{Options.CommandPrefix}roles toggle persistence");
            ResponseAssert.IsSuccess(onAck);
            try
            {
                Assert.Contains("now", onAck.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                var offAck = await RunAdminCommandAsync($"{Options.CommandPrefix}roles toggle persistence");
                ResponseAssert.IsSuccess(offAck);
                Assert.Contains("no longer", offAck.Content, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task TogglePersistence_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles toggle persistence");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateRole_Succeeds_AndIsListed()
        {
            var name = "created-" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles create {name}");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains(name, ack.Content);

                var listResult = await RunAdminCommandAsync($"{Options.CommandPrefix}roles list");
                ResponseAssert.Contains(listResult, name);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {name}");

                var role = Fixture.Admin.Client.GetGuild(Options.GuildId)?.Roles.Values.FirstOrDefault(x => x.Name == name);
                if (role != null)
                    await Fixture.Admin.Client.DeleteRoleAsync(Options.GuildId, role.Id);
            }
        }

        [Fact]
        public async Task CreateRole_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles create nonadmin-{Guid.NewGuid():N}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddRole_Succeeds_AndIsListed()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "add-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}");
                ResponseAssert.IsSuccess(ack);

                var listResult = await RunAdminCommandAsync($"{Options.CommandPrefix}roles list");
                ResponseAssert.Contains(listResult, role.Id.ToString());
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task AddRole_AlreadySelfAssignable_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "dup-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("already self-assignable", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task AddRole_InvokedByNonAdmin_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "nonadmin-" + Guid.NewGuid().ToString("N")[..8]);
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles add {role.Id}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RemoveRole_Succeeds()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "rem-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));

            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles remove {role.Id}");
            ResponseAssert.IsSuccess(ack);
        }

        [Fact]
        public async Task RemoveRole_NotSelfAssignable_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "notset-" + Guid.NewGuid().ToString("N")[..8]);
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles remove {role.Id}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("not self-assignable", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RemoveRole_InvokedByNonManager_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "rem-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            try
            {
                var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles remove {role.Id}");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task ListRoles_WithRoles_ListsThem()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "list-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));

                var result = await RunAdminCommandAsync($"{Options.CommandPrefix}roles list");
                ResponseAssert.Contains(result, role.Id.ToString());
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task ListRoles_InvokedByNonManager_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles list");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddAlias_Succeeds()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "alias-" + Guid.NewGuid().ToString("N")[..8]);
            var alias = "aka" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles alias add {role.Id} {alias}");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains(alias, ack.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task AddAlias_DuplicateAlias_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "alias-" + Guid.NewGuid().ToString("N")[..8]);
            var alias = "aka" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles alias add {role.Id} {alias}"));

                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles alias add {role.Id} {alias}");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("already exists", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task AddAlias_RoleNotSelfAssignable_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "notset-" + Guid.NewGuid().ToString("N")[..8]);
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles alias add {role.Id} somealias");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("not self-assignable", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddAlias_InvokedByNonManager_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "alias-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            try
            {
                var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles alias add {role.Id} somealias");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task RemoveAlias_Succeeds()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "alias-" + Guid.NewGuid().ToString("N")[..8]);
            var alias = "aka" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            try
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles alias add {role.Id} {alias}"));

                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles alias remove {role.Id} {alias}");
                ResponseAssert.IsSuccess(ack);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task RemoveAlias_NotFound_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "alias-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles alias remove {role.Id} nonexistent-alias");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("no alias found", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task RemoveAlias_InvokedByNonManager_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "alias-" + Guid.NewGuid().ToString("N")[..8]);
            var alias = "aka" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles alias add {role.Id} {alias}"));
            try
            {
                var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles alias remove {role.Id} {alias}");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task SetBiasRole_Succeeds()
        {
            await using var primary = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-p-" + Guid.NewGuid().ToString("N")[..8]);
            await using var secondary = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-s-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {primary.Id}"));
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles set secondary {primary.Id} {secondary.Id}");
                ResponseAssert.IsSuccess(ack);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {primary.Id}");
            }
        }

        [Fact]
        public async Task SetBiasRole_SameRole_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-same-" + Guid.NewGuid().ToString("N")[..8]);
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles set secondary {role.Id} {role.Id}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("can't be the same role", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetBiasRole_PrimaryNotSelfAssignable_Fails()
        {
            await using var primary = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-p-" + Guid.NewGuid().ToString("N")[..8]);
            await using var secondary = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-s-" + Guid.NewGuid().ToString("N")[..8]);
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles set secondary {primary.Id} {secondary.Id}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("primary role is not self-assignable", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetBiasRole_SecondaryAlreadySelfAssignable_Fails()
        {
            await using var primary = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-p-" + Guid.NewGuid().ToString("N")[..8]);
            await using var secondary = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-s-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {primary.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {secondary.Id}"));
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles set secondary {primary.Id} {secondary.Id}");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("already self-assignable by itself", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {primary.Id}");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {secondary.Id}");
            }
        }

        [Fact]
        public async Task SetBiasRole_InvokedByNonAdmin_Fails()
        {
            await using var primary = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-p-" + Guid.NewGuid().ToString("N")[..8]);
            await using var secondary = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-s-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {primary.Id}"));
            try
            {
                var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles set secondary {primary.Id} {secondary.Id}");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {primary.Id}");
            }
        }

        [Fact]
        public async Task RolesStats_All_Succeeds()
        {
            var result = await RunAdminCommandAsync($"{Options.CommandPrefix}roles stats all");
            ResponseAssert.Contains(result, "user");
        }

        [Fact]
        public async Task RolesStats_InvokedByAnyone_Succeeds()
        {
            var result = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles stats all");
            ResponseAssert.Contains(result, "user");
        }

        [Fact]
        public async Task Disable_ThenReEnable_Succeeds()
        {
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles disable");
                ResponseAssert.IsSuccess(ack);
            }
            finally
            {
                ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles set channel <#{_roleChannelId}>"));
            }
        }

        [Fact]
        public async Task Disable_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles disable");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GroupAdd_Succeeds()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "grp-" + Guid.NewGuid().ToString("N")[..8]);
            var group = "group" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles group add {group} {role.Id}");
                ResponseAssert.IsSuccess(ack);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task GroupAdd_RoleNotSelfAssignable_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "grp-" + Guid.NewGuid().ToString("N")[..8]);
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles group add somegroup {role.Id}");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("not self-assignable", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GroupAdd_InvokedByNonAdmin_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "grp-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            try
            {
                var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles group add somegroup {role.Id}");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task GroupRemove_Succeeds()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "grp-" + Guid.NewGuid().ToString("N")[..8]);
            var group = "group" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles group add {group} {role.Id}"));
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles group remove {group} {role.Id}");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains(group, ack.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task GroupRemove_InvokedByNonAdmin_Fails()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "grp-" + Guid.NewGuid().ToString("N")[..8]);
            var group = "group" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles group add {group} {role.Id}"));
            try
            {
                var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles group remove {group} {role.Id}");
                ResponseAssert.IsFailure(ack);
                Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task GroupClear_Succeeds()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "grp-" + Guid.NewGuid().ToString("N")[..8]);
            var group = "group" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles group add {group} {role.Id}"));
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles group clear {group}");
                ResponseAssert.IsSuccess(ack);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task GroupClear_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles group clear somegroup");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GroupSetLimit_Succeeds()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "grp-" + Guid.NewGuid().ToString("N")[..8]);
            var group = "group" + Guid.NewGuid().ToString("N")[..8];
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles group add {group} {role.Id}"));
            try
            {
                var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles group set limit {group} 1");
                ResponseAssert.IsSuccess(ack);
                Assert.Contains(group, ack.Content);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task GroupSetLimit_GroupDoesNotExist_Fails()
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles group set limit nonexistent-{Guid.NewGuid():N} 1");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("doesn't exist", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GroupSetLimit_InvokedByNonAdmin_Fails()
        {
            var ack = await RunLowPrivCommandAsync($"{Options.CommandPrefix}roles group set limit somegroup 1");
            ResponseAssert.IsFailure(ack);
            Assert.Contains("permission", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RealRoleChannel_AssignsAndRemovesRoleViaMessage()
        {
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "real-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            try
            {
                var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;

                var beforeAssign = DateTimeOffset.UtcNow;
                await Fixture.LowPriv.Client.SendMessageAsync(_roleChannelId, new LocalMessage().WithContent(role.Name));
                var assignAck = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, _roleChannelId, beforeAssign, RealEffectTimeout);
                Assert.Contains("you now have", assignAck.Content, StringComparison.OrdinalIgnoreCase);

                var memberAfterAssign = await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, lowPrivId);
                Assert.Contains(role.Id, memberAfterAssign!.RoleIds);

                var beforeRemove = DateTimeOffset.UtcNow;
                await Fixture.LowPriv.Client.SendMessageAsync(_roleChannelId, new LocalMessage().WithContent("-" + role.Name));
                var removeAck = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, _roleChannelId, beforeRemove, RealEffectTimeout);
                Assert.Contains("no longer have", removeAck.Content, StringComparison.OrdinalIgnoreCase);

                var memberAfterRemove = await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, lowPrivId);
                Assert.DoesNotContain(role.Id, memberAfterRemove!.RoleIds);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        [Fact]
        public async Task RealRoleChannel_UnrecognizedName_RepliesFailure()
        {
            var before = DateTimeOffset.UtcNow;
            await Fixture.LowPriv.Client.SendMessageAsync(_roleChannelId, new LocalMessage().WithContent("not-a-real-role-" + Guid.NewGuid().ToString("N")[..8]));
            var ack = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, _roleChannelId, before, RealEffectTimeout);
            ResponseAssert.IsFailure(ack);
            Assert.Contains("not a self-assignable role", ack.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Assigning a second primary-bias role while already holding one grants its configured secondary
        /// instead of the primary itself - see SetBiasRole's [Comment] for the full mechanic.
        /// </summary>
        [Fact]
        public async Task RealRoleChannel_BiasRoles_AssignsSecondaryWhenPrimaryAlreadyHeld()
        {
            await using var primaryA = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-pa-" + Guid.NewGuid().ToString("N")[..8]);
            await using var secondaryA = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-sa-" + Guid.NewGuid().ToString("N")[..8]);
            await using var primaryB = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-pb-" + Guid.NewGuid().ToString("N")[..8]);
            await using var secondaryB = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "bias-sb-" + Guid.NewGuid().ToString("N")[..8]);

            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {primaryA.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {primaryB.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles set secondary {primaryA.Id} {secondaryA.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles set secondary {primaryB.Id} {secondaryB.Id}"));
            try
            {
                var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;

                var beforeFirst = DateTimeOffset.UtcNow;
                await Fixture.LowPriv.Client.SendMessageAsync(_roleChannelId, new LocalMessage().WithContent(primaryA.Name));
                await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, _roleChannelId, beforeFirst, RealEffectTimeout);

                var beforeSecond = DateTimeOffset.UtcNow;
                await Fixture.LowPriv.Client.SendMessageAsync(_roleChannelId, new LocalMessage().WithContent(primaryB.Name));
                await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, _roleChannelId, beforeSecond, RealEffectTimeout);

                var member = await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, lowPrivId);
                Assert.Contains(primaryA.Id, member!.RoleIds);
                Assert.Contains(secondaryB.Id, member.RoleIds);
                Assert.DoesNotContain(primaryB.Id, member.RoleIds);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {primaryA.Id}");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {primaryB.Id}");
            }
        }

        /// <summary>
        /// With a group limit of 1, holding one role from the group blocks self-assigning a second one from the
        /// same group - the rejection only looks at roles the group's *existing* members share with the one
        /// being requested, so both roles need to actually be in the group for this to trigger.
        /// </summary>
        [Fact]
        public async Task RealRoleChannel_GroupLimitReached_RejectsAdditionalRoleFromGroup()
        {
            await using var roleA = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "grouplimit-a-" + Guid.NewGuid().ToString("N")[..8]);
            await using var roleB = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "grouplimit-b-" + Guid.NewGuid().ToString("N")[..8]);
            var group = "grouplimit" + Guid.NewGuid().ToString("N")[..8];

            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {roleA.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {roleB.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles group add {group} {roleA.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles group add {group} {roleB.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles group set limit {group} 1"));
            try
            {
                var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;

                var beforeFirst = DateTimeOffset.UtcNow;
                await Fixture.LowPriv.Client.SendMessageAsync(_roleChannelId, new LocalMessage().WithContent(roleA.Name));
                var firstAck = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, _roleChannelId, beforeFirst, RealEffectTimeout);
                Assert.Contains("you now have", firstAck.Content, StringComparison.OrdinalIgnoreCase);

                var beforeSecond = DateTimeOffset.UtcNow;
                await Fixture.LowPriv.Client.SendMessageAsync(_roleChannelId, new LocalMessage().WithContent(roleB.Name));
                var secondAck = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, _roleChannelId, beforeSecond, RealEffectTimeout);
                ResponseAssert.IsFailure(secondAck);
                Assert.Contains($"any more roles from group `{group}`", secondAck.Content, StringComparison.OrdinalIgnoreCase);

                var member = await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, lowPrivId);
                Assert.Contains(roleA.Id, member!.RoleIds);
                Assert.DoesNotContain(roleB.Id, member.RoleIds);
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {roleA.Id}");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {roleB.Id}");
            }
        }

        /// <summary>
        /// Temporarily points the role channel at its own dedicated (never-deleted) channel so enabling
        /// "clearing" here doesn't affect the main role channel other tests rely on.
        /// </summary>
        [Fact]
        public async Task RealRoleChannel_WithClearingEnabled_DeletesMessagesAfterDelay()
        {
            var clearingChannel = await Fixture.Admin.Client.CreateTextChannelAsync(Options.GuildId, "roles-clearing-" + Guid.NewGuid().ToString("N")[..8]);
            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "clear-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles set channel <#{clearingChannel.Id}>"));
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles toggle clearing"));
            try
            {
                var before = DateTimeOffset.UtcNow;
                var triggerMessage = await Fixture.LowPriv.Client.SendMessageAsync(clearingChannel.Id, new LocalMessage().WithContent(role.Name));
                var ack = await Fixture.Admin.WaitForAnyMessageAsync(Options.TargetBotId, clearingChannel.Id, before, RealEffectTimeout);
                Assert.Contains("you now have", ack.Content, StringComparison.OrdinalIgnoreCase);

                await Task.Delay(TimeSpan.FromSeconds(4));
                Assert.Null(await TryFetchMessageAsync(clearingChannel.Id, triggerMessage!.Id));
                Assert.Null(await TryFetchMessageAsync(clearingChannel.Id, ack.Id));
            }
            finally
            {
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles toggle clearing");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles set channel <#{_roleChannelId}>");
                await Fixture.Admin.Client.DeleteChannelAsync(clearingChannel.Id);
            }
        }

        /// <summary>
        /// Grants a role directly via REST (not through the role channel, already covered above) to isolate
        /// HandleUserLeft/HandleUserJoined's persistence logic specifically. Needs a real leave/rejoin cycle -
        /// see the class doc comment.
        /// </summary>
        [Fact]
        public async Task RealMemberRejoin_RestoresPersistentAssignableRoles()
        {
            var lowPrivId = Fixture.LowPriv.Client.CurrentUser!.Id;

            try
            {
                await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, lowPrivId);
            }
            catch (RestApiException ex) when (ex.StatusCode == HttpResponseStatusCode.NotFound)
            {
                Assert.Fail(
                    "The low-privilege tester bot isn't currently in the test guild. Re-invite it using the link " +
                    "this test posted the last time it ran, then run it again.");
                return;
            }

            await using var role = await TemporaryEntity.CreateRoleAsync(Fixture.Admin, Options.GuildId, "persist-" + Guid.NewGuid().ToString("N")[..8]);
            ResponseAssert.IsSuccess(await RunAdminCommandAsync($"{Options.CommandPrefix}roles add {role.Id}"));
            await EnsurePersistenceAsync(enabled: true);
            try
            {
                await Fixture.Admin.Client.GrantRoleAsync(Options.GuildId, lowPrivId, role.Id);

                var inviteUrl = $"https://discord.com/oauth2/authorize?client_id={lowPrivId}&permissions=0&scope=bot" +
                    $"&guild_id={Options.GuildId}&disable_guild_select=true";

                await Fixture.LowPriv.SendMessageAsync(
                    Fixture.ChannelId,
                    "This bot is about to leave the server to test real persistent self-assignable roles. Please " +
                    $"re-invite it within {Options.RejoinTimeout.TotalMinutes:0} minutes using this link: {inviteUrl}");

                await Fixture.LowPriv.Client.LeaveGuildAsync(Options.GuildId);
                await WaitForRejoinAsync(lowPrivId, Options.RejoinTimeout);

                IMember? member = null;
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    member = await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, lowPrivId);
                    if (member!.RoleIds.Contains(role.Id))
                        break;

                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                Assert.Contains(role.Id, member!.RoleIds);
            }
            finally
            {
                await EnsurePersistenceAsync(enabled: false);
                await Fixture.Admin.SendCommandAsync(Fixture.ChannelId, $"{Options.CommandPrefix}roles remove {role.Id}");
            }
        }

        private async Task EnsurePersistenceAsync(bool enabled)
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles toggle persistence");
            var isNowEnabled = !ack.Content.Contains("no longer", StringComparison.OrdinalIgnoreCase);
            if (isNowEnabled != enabled)
                await RunAdminCommandAsync($"{Options.CommandPrefix}roles toggle persistence");
        }

        private async Task EnsureClearingAsync(bool enabled)
        {
            var ack = await RunAdminCommandAsync($"{Options.CommandPrefix}roles toggle clearing");
            var isNowEnabled = ack.Content.Contains("enabled", StringComparison.OrdinalIgnoreCase);
            if (isNowEnabled != enabled)
                await RunAdminCommandAsync($"{Options.CommandPrefix}roles toggle clearing");
        }

        private async Task WaitForRejoinAsync(Snowflake userId, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    await Fixture.Admin.Client.FetchMemberAsync(Options.GuildId, userId);
                    return;
                }
                catch (RestApiException ex) when (ex.StatusCode == HttpResponseStatusCode.NotFound)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            Assert.Fail($"Timed out waiting for the low-privilege tester bot to be re-invited within {timeout.TotalMinutes:0} minutes.");
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
