﻿using System;
using System.Linq;
using Disqord.Bot.Sharding;
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
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace DustyBot.Service
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

        public static void ConfigureBot(this DiscordBotSharderHostingContext configuration, IConfiguration provider)
        {
            var options = provider.Get<DiscordOptions>();
            var botOptions = provider.Get<BotOptions>();
            var webOptions = provider.Get<WebOptions>();

            configuration.Intents = GatewayIntent.DirectReactions |
                GatewayIntent.DirectMessages |
                GatewayIntent.EmojisAndStickers |
                GatewayIntent.Members |
                GatewayIntent.GuildReactions |
                GatewayIntent.GuildMessages |
                GatewayIntent.GuildTyping |
                GatewayIntent.Guilds;

            if (options.TotalShards != null)
            {
                if (options.Shards?.Any() ?? false)
                    configuration.ShardIds = options.Shards.Select(x => new ShardId(x, options.TotalShards.Value));
                else
                    configuration.ShardCount = options.TotalShards;
            }

            configuration.Token = options.BotToken;
            configuration.Prefixes = new[] { botOptions.DefaultCommandPrefix };
            configuration.UseMentionPrefix = true;

            var activity = new LocalActivity($"{botOptions.DefaultCommandPrefix}help | {webOptions.WebsiteShorthand}", 
                Disqord.ActivityType.Listening);

            configuration.Activities = new[] { activity };
        }

        public static void ConfigureCommands(this CommandServiceConfiguration configuration, IConfiguration provider)
        {
            configuration.DefaultArgumentParser = new ArgumentParser();
            configuration.NullableNouns = Enumerable.Empty<string>();
        }

        public static void ConfigureCaching(this DefaultGatewayCacheProviderConfiguration configuration)
        {
            configuration.SupportedTypes.Remove(typeof(CachedSharedUser));

            configuration.SupportedNestedTypes.Remove(typeof(CachedMember));
            configuration.SupportedNestedTypes.Remove(typeof(CachedVoiceState));
            configuration.SupportedNestedTypes.Remove(typeof(CachedPresence));
        }
    }
}
