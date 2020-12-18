using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    internal static class SpotifyHelpers
    {
        public static string BuildAuthorizationHeader(string clientId, string clientSecret) =>
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

        public static async Task<(string Token, int ExpiresIn)> RefreshToken(string refreshToken, string clientId, string clientSecret)
        {
            var request = WebRequest.CreateHttp("https://accounts.spotify.com/api/token");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Headers.Add("Authorization", BuildAuthorizationHeader(clientId, clientSecret));

            using (var writer = new StreamWriter(await request.GetRequestStreamAsync(), Encoding.ASCII))
            {
                await writer.WriteAsync($"grant_type=refresh_token&refresh_token={refreshToken}");
            }

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                var root = JObject.Parse(text);
                return ((string)root["access_token"], (int)root["expires_in"]);
            }
        }

        public static async Task<(string Token, int ExpiresIn)> GetClientToken(string clientId, string clientSecret)
        {
            var request = WebRequest.CreateHttp("https://accounts.spotify.com/api/token");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Headers.Add("Authorization", BuildAuthorizationHeader(clientId, clientSecret));

            using (var writer = new StreamWriter(await request.GetRequestStreamAsync(), Encoding.ASCII))
                await writer.WriteAsync("grant_type=client_credentials");

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = await reader.ReadToEndAsync();
                dynamic root = JObject.Parse(text);
                return ((string)root["access_token"], (int)root["expires_in"]);
            }
        }
    }
}
