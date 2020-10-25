using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public class PolrShortener : IUrlShortener
    {
        private readonly Regex _linkRegex;
        private readonly string _apiKey;
        private readonly string _domain;

        public PolrShortener(string apiKey, string domain)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));
            _linkRegex = new Regex(domain + @"\/[^/?&#]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public bool IsShortened(string url) => _linkRegex.IsMatch(url);

        public async Task<string> ShortenAsync(string url)
        {
            var request = WebRequest.CreateHttp($"{_domain}/api/v2/action/shorten?key={_apiKey}&url={Uri.EscapeDataString(url)}&is_secret=false");

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public async Task<ICollection<string>> ShortenAsync(IEnumerable<string> urls) =>
            await Task.WhenAll(urls.Select(x => ShortenAsync(x)));
    }
}
