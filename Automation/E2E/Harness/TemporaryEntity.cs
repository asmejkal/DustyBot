using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Rest;

namespace DustyBot.Automation.E2E.Harness
{
    /// <summary>
    /// A channel or role created for one test and deleted again via `await using` - covers the "create scratch
    /// state, delete it afterward" pattern used across several test classes. Tests that need extra cleanup
    /// beyond deleting the entity itself (e.g. revoking a granted role) still handle that separately - this only
    /// owns the create/delete pair.
    /// </summary>
    public sealed class TemporaryEntity : IAsyncDisposable
    {
        private readonly Func<Task> _delete;

        private TemporaryEntity(Snowflake id, string name, Func<Task> delete)
        {
            Id = id;
            Name = name;
            _delete = delete;
        }

        public Snowflake Id { get; }

        public string Name { get; }

        public static async Task<TemporaryEntity> CreateChannelAsync(TestDiscordClient client, Snowflake guildId, string name)
        {
            var channel = await client.Client.CreateTextChannelAsync(guildId, name);
            return new TemporaryEntity(channel.Id, name, () => client.Client.DeleteChannelAsync(channel.Id));
        }

        public static async Task<TemporaryEntity> CreateRoleAsync(TestDiscordClient client, Snowflake guildId, string name)
        {
            var role = await client.Client.CreateRoleAsync(guildId, x => x.Name = name);
            return new TemporaryEntity(role.Id, name, () => client.Client.DeleteRoleAsync(guildId, role.Id));
        }

        public ValueTask DisposeAsync() => new(_delete());
    }
}
