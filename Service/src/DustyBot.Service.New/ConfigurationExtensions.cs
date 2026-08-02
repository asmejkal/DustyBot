using System;
using System.Linq;
using Disqord.Bot.Hosting;
using Disqord.Extensions.Interactivity;
using Disqord.Gateway;
using Disqord.Gateway.Api;
using Disqord.Gateway.Default;
using DustyBot.Framework.Commands.Parsing;
using DustyBot.Service.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Qmmands;
using Qmmands.Text;
using Qmmands.Text.Default;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace DustyBot.Service
{
    internal static class ConfigurationExtensions
    {
        public static void ConfigureBotLogging(this LoggerConfiguration configuration, IServiceProvider provider)
        {
            var options = provider.GetRequiredService<IOptions<LoggingOptions>>();
            var discordOptions = provider.GetRequiredService<IOptions<DiscordOptions>>();

            configuration.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Component", "dustybot-service")
                .Enrich.WithProperty("ComponentInstance", $"shard-{string.Join("+", discordOptions.Value.Shards ?? new[] { 0 })}")
                .MinimumLevel.Information()
                .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning);

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

        public static void ConfigureBot(this DiscordBotHostingContext configuration, IConfiguration provider)
        {
            var options = provider.GetSection(ConfigurationSections.Discord).Get<DiscordOptions>();
            var botOptions = provider.GetSection(ConfigurationSections.Bot).Get<BotOptions>();
            var webOptions = provider.GetSection(ConfigurationSections.Web).Get<WebOptions>();

            configuration.Intents = GatewayIntents.DirectReactions |
                GatewayIntents.DirectMessages |
                GatewayIntents.EmojisAndStickers |
                GatewayIntents.Members |
                GatewayIntents.GuildReactions |
                GatewayIntents.GuildMessages |
                GatewayIntents.GuildTyping |
                GatewayIntents.Guilds |
                GatewayIntents.MessageContent;

            if (options.TotalShards != null)
            {
                var shardIds = options.Shards?.Any() ?? false
                    ? options.Shards.Select(x => new ShardId(x, options.TotalShards.Value))
                    : Enumerable.Range(0, options.TotalShards.Value).Select(x => new ShardId(x, options.TotalShards.Value));

                configuration.CustomShardSet = new ShardSet(shardIds);
            }

            configuration.Token = options.Token;
            configuration.Prefixes = new[] { botOptions.DefaultCommandPrefix };
            configuration.UseMentionPrefix = false;

            configuration.ServiceAssemblies = null;
        }

        public static void ConfigureCommands(this IServiceCollection services)
        {
            services.AddSingleton<IArgumentParserProvider>(_ =>
            {
                var provider = new DefaultArgumentParserProvider();
                provider.Add(new ArgumentParser());
                return provider;
            });
        }

        public static void ConfigureCaching(this DefaultGatewayCacheProviderConfiguration configuration)
        {
            configuration.MessagesPerChannel = 100;

            // configuration.SupportedTypes.Remove(typeof(CachedSharedUser));

            // configuration.SupportedNestedTypes.Remove(typeof(CachedMember));
            configuration.SupportedNestedTypes.Remove(typeof(CachedVoiceState));
            configuration.SupportedNestedTypes.Remove(typeof(CachedPresence));
        }

        public static void ConfigureInteractivity(this InteractivityExtensionConfiguration configuration)
        {
            configuration.DefaultMenuTimeout = TimeSpan.FromHours(2);
        }
    }
}
