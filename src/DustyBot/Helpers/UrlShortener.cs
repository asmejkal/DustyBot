using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public static class UrlShortener
    {
        public static async Task<string> ShortenUrl(string url, string BitlyApiKey)
        {
            var request = WebRequest.CreateHttp("https://api-ssl.bitly.com/v4/bitlinks");
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Headers["Authorization"] = "Bearer " + BitlyApiKey;

            using (var writer = new StreamWriter(await request.GetRequestStreamAsync(), Encoding.ASCII))
                await writer.WriteAsync("{\"long_url\": \"" + url + "\"}").ConfigureAwait(false);

            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                var content = await reader.ReadToEndAsync().ConfigureAwait(false);
                var o = JObject.Parse(content);

                return (string)o["link"];
            }
        }

        static readonly Regex LinkRegex = new Regex(@"http[s]?://bit.ly/\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static bool IsShortenedLink(string s) => LinkRegex.IsMatch(s);
    }
}
