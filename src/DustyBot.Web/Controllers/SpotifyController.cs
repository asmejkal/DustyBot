using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Database.Services;
using DustyBot.Database.Services.Configuration;
using DustyBot.Database.TableStorage.Tables;
using DustyBot.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace DustyBot.Web.Controllers
{
    public class SpotifyController : Controller
    {
        private static readonly TimeSpan StateCookieLifetime = TimeSpan.FromHours(1);
        
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SpotifyController> _logger;

        public SpotifyController(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, ILogger<SpotifyController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var userIdClaim = User.FindFirst(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var userNameClaim = User.FindFirst(c => c.Type == ClaimTypes.Name)?.Value;
            if (userIdClaim == null || userNameClaim == null)
                return Challenge(new AuthenticationProperties() { RedirectUri = Url.Action("Index", "Spotify"), Parameters = { { "prompt", "consent" } } });

            ViewData["UserName"] = userNameClaim;
            return View();
        }

        [Authorize]
        public IActionResult Logout()
        {
            HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Spotify");
        }

        [Authorize]
        public IActionResult ConnectBegin()
        {
            var userIdClaim = User.FindFirst(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var userNameClaim = User.FindFirst(c => c.Type == ClaimTypes.Name)?.Value;

            var id = ulong.Parse(userIdClaim);

            _logger.LogInformation($"Initiating Spotify OAuth2 flow for user {id}");

            var clientId = Environment.GetEnvironmentVariable("SpotifyClientId");

            var scopes = new[]
            {
                "user-read-currently-playing",
                "user-top-read",
                "user-read-recently-played"
            };

            var cookieOptions = new CookieOptions()
            {
                Path = "/",
                Expires = DateTimeOffset.Now + StateCookieLifetime,
                MaxAge = StateCookieLifetime,
                HttpOnly = true,
                Secure = true
            };

            var redirectUri = GetSpotifyRedirectUri();
            var state = GenerateState(id);
            Response.Cookies.Append("SpotifyOAuth2State", state, cookieOptions);
            return Redirect($"https://accounts.spotify.com/authorize?client_id={clientId}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={state}&scope={string.Join(" ", scopes)}&show_dialog=true");
        }

        [Authorize]
        public async Task<IActionResult> ConnectLanding()
        {
            _logger.LogInformation("Received Spotify OAuth2 authorization code");

            if (!Request.Cookies.TryGetValue("SpotifyOAuth2State", out var state))
                return BadRequest("Your session has expired, please try again.");

            Response.Cookies.Delete("SpotifyOAuth2State");

            if (Request.Query["state"] != state)
                return BadRequest("Invalid state");

            var userIdClaim = User.FindFirst(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var userNameClaim = User.FindFirst(c => c.Type == ClaimTypes.Name)?.Value;
            var id = DecodeState(state);
            if (id != ulong.Parse(userIdClaim))
                return BadRequest("Invalid user");

            if (Request.Query.ContainsKey("error"))
            {
                _logger.LogError($"Spotify OAuth2 authorization code error: {Request.Query["error"]}");
                return BadRequest("Spotify returned an error.");
            }

            if (!Request.Query.TryGetValue("code", out var code))
            {
                _logger.LogError($"Spotify OAuth2 authorization code missing");
                return BadRequest("Spotify returned an invalid response.");
            }

            string refreshToken;
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token"))
            {
                var content = new Dictionary<string, string>()
                {
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "redirect_uri", GetSpotifyRedirectUri() },
                    { "client_id", Environment.GetEnvironmentVariable("SpotifyClientId") },
                    { "client_secret", Environment.GetEnvironmentVariable("SpotifyClientSecret") },
                };

                request.Content = new FormUrlEncodedContent(content);
                using (var response = await _httpClientFactory.CreateClient().SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Failed to get Spotify token: {response.StatusCode}");
                        return Error();
                    }

                    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
                    refreshToken = (string)result["refresh_token"];
                    if (refreshToken == null)
                    {
                        _logger.LogError($"Didn't receive Spotify token");
                        return Error();
                    }
                }
            }

            var account = new SpotifyAccount()
            {
                UserId = id.ToString(),
                RefreshToken = refreshToken
            };

            var options = Options.Create(new DatabaseOptions() { TableStorageConnectionString = Environment.GetEnvironmentVariable("TableStorageConnectionString") });
            var service = new SpotifyAccountsService(options);
            await service.AddOrUpdateUserAccountAsync(account, CancellationToken.None);

            ViewData["UserName"] = userNameClaim;
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private static string GenerateState(ulong id)
        {
            var random = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(random);

            return WebEncoders.Base64UrlEncode(BitConverter.GetBytes(id).Concat(random).ToArray());
        }

        private static ulong DecodeState(string state)
        {
            return BitConverter.ToUInt64(WebEncoders.Base64UrlDecode(state));
        }

        private string GetSpotifyRedirectUri()
        {
            var builder = new UriBuilder(UriHelper.GetEncodedUrl(_httpContextAccessor.HttpContext.Request))
            {
                Path = Url.Action("ConnectLanding", "Spotify"),
                Query = ""
            };

            if (builder.Port == 443 || builder.Port == 80)
                builder.Port = -1;

            return builder.ToString();
        }
    }
}
