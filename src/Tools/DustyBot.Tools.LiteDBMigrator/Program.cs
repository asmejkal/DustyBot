using CommandLine;
using DustyBot.Framework.LiteDB;
using DustyBot.Framework.Settings;
using DustyBot.Settings;
using DustyBot.Settings.LiteDB;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DustyBot.Tools.LiteDBMigrator
{
    class Program
    {
        [Verb("migrate", HelpText = "Migrate old LiteDB database to MongoDB.")]
        public class LiteDbMigrateOptions
        {
            [Value(0, MetaName = "Instance", Required = true, HelpText = "Instance name.")]
            public string Instance { get; set; }

            [Value(1, MetaName = "LiteDbPassword", Required = true, HelpText = "Password for database decryption.")]
            public string LiteDbPassword { get; set; }

            [Value(2, MetaName = "MongoDbConnectionString", Required = true, HelpText = "MongoDb connection string for this instance.")]
            public string MongoDbConnectionString { get; set; }
        }

        public const string DataFolder = "Data";
        public const ushort SettingsVersion = 12;
        public static string GetInstanceDbPath(string instance) => Path.Combine(DataFolder, instance + ".db");

        static int Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<LiteDbMigrateOptions>(args)
                .MapResult(
                    (LiteDbMigrateOptions opts) => new Program().RunLiteDbMigrate(opts).GetAwaiter().GetResult(),
                    errs => 1);

            return result;
        }

        public async Task<int> RunLiteDbMigrate(LiteDbMigrateOptions opts)
        {
            try
            {
                var instancePath = GetInstanceDbPath(opts.Instance);
                if (!File.Exists(instancePath))
                    throw new InvalidOperationException($"Instance {opts.Instance} not found.");

                using (var liteDbSettings = new SettingsProvider(instancePath, new Migrator(SettingsVersion, new Migrations()), opts.LiteDbPassword))
                {
                    var mongoDbSettings = new MongoDbSettingsProvider(opts.MongoDbConnectionString, opts.Instance);

                    async Task MigrateServerSettings<T>()
                        where T : IServerSettings
                    {
                        var settings = await liteDbSettings.Read<T>();
                        foreach (var setting in settings)
                            await mongoDbSettings.Set(setting);
                    }

                    async Task MigrateUserSettings<T>()
                        where T : IUserSettings
                    {
                        var settings = await liteDbSettings.ReadUser<T>();
                        foreach (var setting in settings)
                            await mongoDbSettings.SetUser(setting);
                    }

                    async Task MigrateGlobalSettings<T>()
                        where T : new()
                    {
                        var settings = await liteDbSettings.ReadGlobal<T>();
                        await mongoDbSettings.SetGlobal(settings);
                    }

                    await MigrateGlobalSettings<BotConfig>();
                    await MigrateServerSettings<EventsSettings>();
                    await MigrateUserSettings<LastFmUserSettings>();
                    await MigrateServerSettings<LogSettings>();
                    await MigrateServerSettings<MediaSettings>();
                    await MigrateServerSettings<NotificationSettings>();
                    await MigrateServerSettings<PollSettings>();
                    await MigrateServerSettings<RaidProtectionSettings>();
                    await MigrateServerSettings<ReactionsSettings>();
                    await MigrateServerSettings<RolesSettings>();
                    await MigrateServerSettings<ScheduleSettings>();
                    await MigrateServerSettings<StarboardSettings>();
                    await MigrateGlobalSettings<SupporterSettings>();
                    await MigrateUserSettings<UserCredentials>();
                    await MigrateUserSettings<UserMediaSettings>();
                    await MigrateUserSettings<UserNotificationSettings>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failure: " + ex.ToString());
            }

            return 0;
        }
    }
}
