using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Settings
{
    public class SettingsCleaner : Events.EventHandler
    {
        public ISettingsProvider Settings { get; set; }
        public Logging.ILogger Logger { get; set; }

        public SettingsCleaner(ISettingsProvider settings, Logging.ILogger logger)
        {
            Settings = settings;
            Logger = logger;
        }

        public override Task OnLeftGuild(SocketGuild guild)
        {
            Utility.TaskHelper.FireForget(async () =>
            {
                try
                {
                    await Settings.DeleteServer(guild.Id);
                }
                catch (Exception ex)
                {
                    await Logger.Log(new Discord.LogMessage(Discord.LogSeverity.Error, "Cleaner", "Failed to delete server settings.", ex));
                }
            });

            return Task.CompletedTask;
        }
    }
}
