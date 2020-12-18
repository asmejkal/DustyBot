namespace DustyBot.Framework.Configuration
{
    public sealed class FrameworkGuildConfig
    {
        public string CustomCommandPrefix { get; }

        public FrameworkGuildConfig(string customCommandPrefix)
        {
            CustomCommandPrefix = customCommandPrefix;
        }
    }
}
