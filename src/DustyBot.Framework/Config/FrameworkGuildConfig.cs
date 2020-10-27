using System;

namespace DustyBot.Framework.Config
{
    public class FrameworkGuildConfig
    {
        public string CustomCommandPrefix { get; }

        public FrameworkGuildConfig(string customCommandPrefix)
        {
            CustomCommandPrefix = customCommandPrefix;
        }
    }
}
