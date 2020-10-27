using System.Threading.Tasks;

namespace DustyBot.Framework.Config
{
    public interface IFrameworkGuildConfigProvider
    {
        Task<FrameworkGuildConfig> GetConfigAsync(ulong guildId);
    }
}
