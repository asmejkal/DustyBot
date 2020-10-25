using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public class DefaultShortener : IUrlShortener
    {
        public bool IsShortened(string url) => true;

        public Task<string> ShortenAsync(string url) => Task.FromResult(url);

        public Task<ICollection<string>> ShortenAsync(IEnumerable<string> urls) => Task.FromResult<ICollection<string>>(urls.ToList());
    }
}
