using System.Threading.Tasks;

namespace DustyBot.Framework.Configuration
{
    public interface IFrameworkGuildConfigProvider
    {
        Task<FrameworkGuildConfig> GetConfigAsync(ulong guildId);
    }
}
