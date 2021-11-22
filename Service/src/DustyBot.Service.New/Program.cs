using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Disqord.Bot.Hosting;
using DustyBot.Framework;
using DustyBot.Service.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DustyBot.Service
{
    internal static class Program
    {
        public enum ReturnCode
        {
            Success,
            GeneralFailure
        }

        public static async Task<int> Main()
        {
            try
            {
                await RunAsync(CreateConsoleLifetimeToken());
            }
            catch (OperationCanceledException)
            {
                // Stopped
            }
            catch (Exception ex)
            {
                Console.WriteLine("Top level exception: " + ex.ToString());
                return (int)ReturnCode.GeneralFailure;
            }

            return (int)ReturnCode.Success;
        }

        private static async Task RunAsync(CancellationToken ct)
        {
            await WaitUntilElasticsearchReady(TimeSpan.FromMinutes(1), ct);

            using var host = new HostBuilder()
                .ConfigureAppConfiguration(x => x.AddEnvironmentVariables())
                .ConfigureServices((context, services) => services.AddBotServices(context.Configuration))
                .ConfigureDiscordBotSharder<DustyBotSharder>((context, config) => config.ConfigureBot(context.Configuration))
                .UseSerilog((_, provider, config) => config.ConfigureBotLogging(provider))
                .Build();

            await host.StartAsync(ct);
            await host.WaitForShutdownAsync(ct);
        }

        private static async Task WaitUntilElasticsearchReady(TimeSpan timeout, CancellationToken ct)
        {
            var options = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build()
                .Get<LoggingOptions>();

            if (!string.IsNullOrEmpty(options.ElasticsearchNodeUri))
            {
                using var client = new TcpClient();
                var uri = new UriBuilder(options.ElasticsearchNodeUri);
                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed < timeout)
                {
                    try
                    {
                        await client.ConnectAsync(uri.Host, uri.Port);
                        return;
                    }
                    catch (SocketException ex)
                    {
                        var delay = TimeSpan.FromSeconds(3);
                        Console.WriteLine($"Elasticsearch not ready ({ex.SocketErrorCode}), retrying after {delay}");
                        await Task.Delay(delay, ct);
                    }
                }

                throw new TimeoutException("Failed to connect to Elasticsearch");
            }
        }

        private static CancellationToken CreateConsoleLifetimeToken()
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            AppDomain.CurrentDomain.ProcessExit += (_, e) => cts.Cancel();
            return cts.Token;
        }
    }
}
