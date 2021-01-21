using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DustyBot.Core.Async;
using DustyBot.Core.Net;
using DustyBot.Core.Services;
using DustyBot.Database.Services;
using DustyBot.Service.Configuration;
using DustyBot.Service.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace DustyBot.Service.Services
{
    internal sealed class RotatingProxyService : RecurringTaskService, IProxyService, IDisposable
    {
        private static readonly TimeSpan ProxyListRefreshPeriod = TimeSpan.FromHours(1);

        private readonly string _token;
        private readonly Uri _proxyListUrl;
        private readonly IProxyListService _proxyList;
        private readonly ILogger<RotatingProxyService> _logger;

        private readonly SemaphoreSlim _proxiesLock = new SemaphoreSlim(1, 1);
        private int _proxyCounter;
        private ImmutableList<WebProxy> _proxies;

        public RotatingProxyService(IOptions<IntegrationOptions> options, IProxyListService proxyList, ILogger<RotatingProxyService> logger, ITimerAwaiter timerAwaiter, IServiceProvider services)
            : base(ProxyListRefreshPeriod, timerAwaiter, services, logger)
        {
            _token = options.Value.ProxyListToken ?? throw new ArgumentNullException();
            _proxyListUrl = new Uri(options.Value.ProxyListUrl ?? throw new ArgumentNullException());
            _proxyList = proxyList ?? throw new ArgumentNullException(nameof(proxyList));
            _logger = logger;
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
                _logger.LogInformation("Blacklisted proxy {Proxy} until {Date}, {Remaining} proxies remaining", proxy.Address.AbsoluteUri, DateTime.UtcNow + duration, _proxies.Count);
            }
        }

        public Task ForceRefreshAsync() => ExecuteAsync(default);

        public override void Dispose()
        {
            ((IDisposable)_proxiesLock)?.Dispose();
            base.Dispose();
        }

        protected override async Task ExecuteRecurringAsync(IServiceProvider provider, int executionCount, CancellationToken ct)
        {
            var proxies = new List<WebProxy>();
            for (int i = 1; ; i++)
            {
                var request = WebRequest.CreateHttp(new UriBuilder(_proxyListUrl) { Query = $"?page={i}" }.Uri);
                request.Headers.Add("Authorization", $"Token {_token}");

                using var response = await request.GetResponseAsync(ct);
                using var reader = new StreamReader(response.GetResponseStream());

                var text = await reader.ReadToEndAsync();
                var root = JObject.Parse(text);

                foreach (var result in (JArray)root["results"])
                {
                    var credentials = new NetworkCredential((string)result["username"], (string)result["password"]);
                    proxies.Add(new WebProxy(new UriBuilder("http", (string)result["proxy_address"], (int?)result["ports"]?["http"] ?? 80).Uri) { Credentials = credentials });
                }

                if ((string)root["next"] == null)
                    break;
            }

            using (await _proxiesLock.ClaimAsync(ct))
            {
                var blacklist = await _proxyList.GetBlacklistAsync();
                _proxies = proxies.Where(x => !blacklist.Any(y => y.Address == x.Address.AbsoluteUri)).ToImmutableList();
                _logger.LogInformation("Downloaded proxies ({BlacklistCount} blacklisted): {ProxyList}", proxies.Count - _proxies.Count, string.Join(", ", proxies.Select(x => x.Address)));
            }
        }
    }
}
