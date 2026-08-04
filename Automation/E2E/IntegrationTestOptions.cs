using System;
using Disqord;

namespace DustyBot.Automation.E2E
{
    /// <summary>
    /// Configuration for the integration test suite, read from environment variables. See README.md for the
    /// one-time test guild setup these values refer to.
    /// </summary>
    public class IntegrationTestOptions
    {
        public required string AdminTesterBotToken { get; init; }

        public required string LowPrivTesterBotToken { get; init; }

        public required Snowflake TargetBotId { get; init; }

        public required Snowflake GuildId { get; init; }

        public required string CommandPrefix { get; init; }

        /// <summary>
        /// How long to wait for a person to click the low-privilege tester bot's re-invite link during
        /// ByeCommandTests.RealMemberLeaveAndRejoin_TriggersConfiguredByeAndGreetMessages. Defaults to 10 minutes.
        /// </summary>
        public required TimeSpan RejoinTimeout { get; init; }

        /// <summary>
        /// How long to wait for a person to connect a real Spotify account via the "sf np" connect link and reply
        /// in the run channel during SpotifyModuleTests' real-account tests. Defaults to 10 minutes.
        /// </summary>
        public required TimeSpan SpotifyConnectTimeout { get; init; }

        public static IntegrationTestOptions FromEnvironment()
        {
            return new IntegrationTestOptions
            {
                AdminTesterBotToken = RequireString("IntegrationTests__AdminTesterBotToken"),
                LowPrivTesterBotToken = RequireString("IntegrationTests__LowPrivTesterBotToken"),
                TargetBotId = RequireSnowflake("IntegrationTests__TargetBotId"),
                GuildId = RequireSnowflake("IntegrationTests__GuildId"),
                CommandPrefix = RequireString("IntegrationTests__CommandPrefix"),
                RejoinTimeout = TimeSpan.FromSeconds(OptionalInt("IntegrationTests__RejoinTimeoutSeconds") ?? 600),
                SpotifyConnectTimeout = TimeSpan.FromSeconds(OptionalInt("IntegrationTests__SpotifyConnectTimeoutSeconds") ?? 600),
            };
        }

        private static string RequireString(string variable)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Environment variable '{variable}' is required to run the integration test suite. See README.md.");

            return value;
        }

        private static Snowflake RequireSnowflake(string variable)
            => new(ulong.Parse(RequireString(variable)));

        private static int? OptionalInt(string variable)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrWhiteSpace(value) ? null : int.Parse(value);
        }
    }
}
