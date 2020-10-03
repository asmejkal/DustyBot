using Discord.Rest;
using System.Threading.Tasks;

namespace DustyBot.Framework.Utility
{
    public interface IUserFetcher
    {
        Task<RestUser> FetchUserAsync(ulong id);
        Task<RestGuildUser> FetchGuildUserAsync(ulong guildId, ulong id);
    }
}
