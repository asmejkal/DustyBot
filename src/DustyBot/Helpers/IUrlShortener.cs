using System.Collections.Generic;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public interface IUrlShortener
    {
        Task<string> ShortenAsync(string url);
        Task<ICollection<string>> ShortenAsync(IEnumerable<string> urls);
        bool IsShortened(string url);
    }
}
