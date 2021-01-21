using System;
using System.Threading.Tasks;
using DustyBot.Framework;
using DustyBot.Service.Configuration;
using DustyBot.Service.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace DustyBot
{
    internal static class Program
    {
        public enum ReturnCode
        {
            Success,
            GeneralFailure
        }

        public static async Task<int> Main(string[] args) => (int)await RunAsync();

        public static async Task<ReturnCode> RunAsync()
        {
            try
            {
                using var host = new HostBuilder()
                    .ConfigureAppConfiguration(x => x.AddEnvironmentVariables())
                    .ConfigureServices((context, services) => services.AddBotServices(context.Configuration))
                    .UseSerilog((_, provider, config) => config.ConfigureBotLogging(provider.GetRequiredService<IOptions<LoggingOptions>>()))
                    .UseConsoleLifetime()
                    .Build();

                await host.Services.GetRequiredService<DiscordClientLauncher>().LaunchAsync();
                await host.StartAsync();
                await host.Services.GetRequiredService<IFramework>().StartAsync(default);

                await host.WaitForShutdownAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Top level exception: " + ex.ToString());
                return ReturnCode.GeneralFailure;
            }

            return ReturnCode.Success;
        }
    }
}
