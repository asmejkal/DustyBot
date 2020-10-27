using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public class BitlyUrlShortener : IUrlShortener
    {
        private static readonly Regex LinkRegex = new Regex(@"http[s]?://bit.ly/\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly string _apiKey;

        public BitlyUrlShortener(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        }

        public bool IsShortened(string url) => LinkRegex.IsMatch(url);

        public async Task<string> ShortenAsync(string url)
        {
            var request = WebRequest.CreateHttp("https://api-ssl.bitly.com/v4/bitlinks");
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Headers["Authorization"] = "Bearer " + _apiKey;

            using (var writer = new StreamWriter(await request.GetRequestStreamAsync(), Encoding.ASCII))
                await writer.WriteAsync("{\"long_url\": \"" + url + "\"}");

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                var content = await reader.ReadToEndAsync();
                var o = JObject.Parse(content);

                return (string)o["link"];
            }
        }

        public async Task<ICollection<string>> ShortenAsync(IEnumerable<string> urls) => 
            await Task.WhenAll(urls.Select(x => ShortenAsync(x)));
    }
}
