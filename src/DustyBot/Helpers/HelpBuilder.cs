using Discord;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Settings;
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    static class HelpBuilder
    {
        public const string WebsiteRoot = "http://dustybot.info";
        public const string WebsiteShorthand = "dustybot.info";
        public const string ReferencePath = "/reference";
        public const string ScheduleGuidePath = "/schedule";
        public const string ImagesPath = "/img";

        public const string ReferenceUrl = WebsiteRoot + ReferencePath;
        public const string ScheduleGuideUrl = WebsiteRoot + ScheduleGuidePath;
        public const string ImagesFolderUrl = WebsiteRoot + ImagesPath;
        public const string LfIconUrl = ImagesFolderUrl + "/lf.png";

        public const string SupportServerInvite = "https://discord.gg/mKKJFvZ";

        public static string GetModuleWebAnchor(IModule module) => module.Name.Replace(' ', '-').ToLowerInvariant();
        public static string GetModuleWebLink(IModule module) => DiscordHelpers.SanitiseMarkdownUri(ReferenceUrl + "#" + GetModuleWebAnchor(module));

        public static async Task<Embed> GetModuleHelpEmbed(IModule module, ISettingsProvider settings)
        {
            var config = await settings.ReadGlobal<BotConfig>();
            return new EmbedBuilder()
                .WithDescription($"● For a list of commands in this module, please see the [website]({GetModuleWebLink(module)}).\n● Type `{config.CommandPrefix}help command name` to see quick help for a specific command.\n\nIf you need further assistance or have any questions, please join the [support server]({SupportServerInvite}).")
                .WithTitle($"❔ {module.Name} help").Build();
        }

        public static async Task<EmbedBuilder> GetHelpEmbed(ISettingsProvider settings)
        {
            var config = await settings.ReadGlobal<BotConfig>();
            return new EmbedBuilder()
                    .WithDescription($"● For a full list of commands see the [website]({ReferenceUrl}).\n● Type `{config.CommandPrefix}help command name` to see quick help for a specific command.\n\nIf you need further assistance or have any questions, please join the [support server]({SupportServerInvite}).")
                    .WithTitle("❔ Help");
        }
    }
}
