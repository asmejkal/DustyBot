using System.Threading.Tasks;

namespace DustyBot.Framework.Configuration
{
    internal class DefaultFrameworkGuildConfigProvider : IFrameworkGuildConfigProvider
    {
        public Task<FrameworkGuildConfig> GetConfigAsync(ulong guildId) => 
            Task.FromResult<FrameworkGuildConfig>(null);
    }
}
