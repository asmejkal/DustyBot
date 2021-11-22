using System;
using System.Linq;
using Disqord.Gateway;
using Disqord.Gateway.Api;
using Disqord.Sharding;
using DustyBot.Service.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace DustyBot
{
    internal static class ServicesConfigurationExtensions
    {
        public static void ConfigureBotLogging(this LoggerConfiguration configuration, IServiceProvider provider)
        {
            var options = provider.GetRequiredService<IOptions<LoggingOptions>>();
            var discordOptions = provider.GetRequiredService<IOptions<DiscordOptions>>();

            configuration.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Component", "dustybot-service")
                .Enrich.WithProperty("ComponentInstance", $"shard-{string.Join("+", discordOptions.Value.Shards ?? new[] { 0 })}")
                .MinimumLevel.Information();

            if (!string.IsNullOrEmpty(options.Value.ElasticsearchNodeUri))
            {
                configuration.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(options.Value.ElasticsearchNodeUri))
                {
                    IndexFormat = "dustybot-{0:yyyy-MM-dd}",
                    AutoRegisterTemplate = true,
                    AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                    DetectElasticsearchVersion = true,
                    RegisterTemplateFailure = RegisterTemplateRecovery.FailSink,
                    EmitEventFailure = EmitEventFailureHandling.ThrowException
                });
            }
        }

        public static void ConfigureDiscordClient(this DiscordClientSharderHostingContext config, IConfiguration configuration)
        {
            var options = configuration.Get<DiscordOptions>();
            var botOptions = configuration.Get<BotOptions>();
            var webOptions = configuration.Get<WebOptions>();

            config.Intents = GatewayIntent.DirectReactions |
                GatewayIntent.DirectMessages |
                GatewayIntent.EmojisAndStickers |
                GatewayIntent.Members |
                GatewayIntent.GuildReactions |
                GatewayIntent.GuildMessages |
                GatewayIntent.GuildTyping |
                GatewayIntent.Guilds;

            if (options.TotalShards != null)
            {
                if (options.Shards.Any())
                    config.ShardIds = options.Shards.Select(x => new ShardId(x, options.TotalShards.Value));
                else
                    config.ShardCount = options.TotalShards;
            }

            config.ServiceAssemblies = null;
            config.Token = options.BotToken;

            var activity = new LocalActivity($"{botOptions.DefaultCommandPrefix}help | {webOptions.WebsiteShorthand}", 
                Disqord.ActivityType.Listening);

            config.Activities = new[] { activity };
        }
    }
}
