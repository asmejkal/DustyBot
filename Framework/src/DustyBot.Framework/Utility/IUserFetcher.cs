using System.Threading.Tasks;
using Discord.Rest;

namespace DustyBot.Framework.Utility
{
    public interface IUserFetcher
    {
        Task<RestUser> FetchUserAsync(ulong id);
        Task<RestGuildUser> FetchGuildUserAsync(ulong guildId, ulong id);
    }
}
