using Discord;
using DustyBot.Core.Async;
using DustyBot.Database.Services;
using DustyBot.Exceptions;
using DustyBot.Framework.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Services
{
    internal sealed class RotatingProxyService : BaseRecurringTaskService, IProxyService, IDisposable
    {
        private static readonly TimeSpan ProxyListRefreshPeriod = TimeSpan.FromHours(1);

        private readonly string _token;
        private readonly Uri _proxyListUrl;
        private readonly IProxyListService _proxyList;
        private readonly ILogger _logger;

        private int _proxyCounter;
        private readonly SemaphoreSlim _proxiesLock = new SemaphoreSlim(1, 1);
        private ImmutableList<WebProxy> _proxies;

        public RotatingProxyService(string token, Uri proxyListUrl, IProxyListService proxyList, ILogger logger)
            : base(ProxyListRefreshPeriod, logger)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _proxyListUrl = proxyListUrl ?? throw new ArgumentNullException(nameof(proxyListUrl));
            _proxyList = proxyList ?? throw new ArgumentNullException(nameof(proxyList));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WebProxy> GetProxyAsync()
        {
            await WaitForFirstCompletion();

            var proxies = _proxies;
            if (!proxies.Any())
                throw new ProxiesDepletedException();

            return proxies[Interlocked.Increment(ref _proxyCounter) % proxies.Count];
        }

        public async Task BlacklistProxyAsync(WebProxy proxy, TimeSpan duration)
        {
            using (await _proxiesLock.ClaimAsync())
            {
                if (_proxies != null)
                    _proxies = _proxies.Remove(proxy);

                await _proxyList.BlacklistProxyAsync(proxy.Address.AbsoluteUri, duration);
                await _logger.Log(new LogMessage(LogSeverity.Info, "Service", $"Blacklisted proxy {proxy.Address.AbsoluteUri} until {DateTime.UtcNow + duration}, {_proxies.Count} proxies remaining."));
            }
        }

        public Task ForceRefreshAsync() => ExecuteAsync(default);

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            var request = WebRequest.CreateHttp(_proxyListUrl);
            request.Headers.Add("Authorization", $"Token {_token}");

            using var response = await request.GetResponseAsync();
            using var reader = new StreamReader(response.GetResponseStream());

            var text = await reader.ReadToEndAsync();
            var root = JObject.Parse(text);

            var proxies = new List<WebProxy>();
            foreach (var result in (JArray)root["results"])
            {
                var credentials = new NetworkCredential((string)result["username"], (string)result["password"]);
                proxies.Add(new WebProxy(new UriBuilder("http", (string)result["proxy_address"]).Uri) { Credentials = credentials });
            }

            using (await _proxiesLock.ClaimAsync(ct))
            {
                var blacklist = await _proxyList.GetBlacklistAsync();
                _proxies = proxies.Where(x => !blacklist.Any(y => y.Address == x.Address.AbsoluteUri)).ToImmutableList();
                await _logger.Log(new LogMessage(LogSeverity.Info, "Service", $"Downloaded proxies ({proxies.Count - _proxies.Count} blacklisted): {string.Join(", ", proxies.Select(x => x.Address))}"));
            }
        }

        protected override void Dispose(bool disposing)
        {
            ((IDisposable)_proxiesLock)?.Dispose();
            base.Dispose(disposing);
        }
    }
}
