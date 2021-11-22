using System.Threading.Tasks;

namespace DustyBot.Framework.Configuration
{
    /// <summary>
    /// Provides guild-specific framework configuration.
    /// </summary>
    public interface IFrameworkGuildConfigProvider
    {
        /// <summary>
        /// Gets guild-specific framework configuration.
        /// </summary>
        /// <param name="guildId">ID of the guild</param>
        /// <returns>guild-specific framework configuration</returns>
        Task<FrameworkGuildConfig> GetConfigAsync(ulong guildId);
    }
}
