using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    class SpotifyClient
    {
        private string ClientId { get; }
        private string ClientSecret { get; }
        private (string token, DateTimeOffset expires) Authorization { get; set; }

        private SpotifyClient(string clientId, string clientSecret)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
        }

        public static async Task<SpotifyClient> Create(string clientId, string clientSecret)
        {
            var client = new SpotifyClient(clientId, clientSecret);
            await client.Authorize();
            return client;
        }

        private async Task Authorize()
        {
            var request = WebRequest.CreateHttp("https://accounts.spotify.com/api/token");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));

            using (var writer = new StreamWriter(await request.GetRequestStreamAsync(), Encoding.ASCII))
                await writer.WriteAsync("grant_type=client_credentials").ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                dynamic root = JObject.Parse(text);
                Authorization = ((string)root.access_token, now.AddSeconds((int)root.expires_in));
            }
        }

        private async Task<string> GetToken()
        {
            if (Authorization.token == null || DateTimeOffset.Now + TimeSpan.FromMinutes(1) > Authorization.expires)
                await Authorize();

            return Authorization.token;
        }

        public async Task<string> SearchTrackUrl(string query)
        {
            var request = WebRequest.CreateHttp($"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(query)}&type=track&limit=1");
            request.Headers.Add("Authorization", $"Bearer {await GetToken()}");

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                dynamic root = JObject.Parse(text);
                dynamic item = (root.tracks?.items as JArray)?.FirstOrDefault();
                return (string)item?.external_urls?.spotify;
            }
        }
    }
}
