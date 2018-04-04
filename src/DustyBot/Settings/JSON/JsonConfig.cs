using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Settings.JSON
{
    class JsonConfig : IOwnerConfig
    {
        public const string FileName = "OwnerConfig.json";
        public string FilePath { get { return Path.Combine(Definitions.GlobalDefinitions.DataFolder, FileName); } }

        public string BotToken => (string)_data["BotToken"];
        public IReadOnlyCollection<ulong> OwnerIDs => ((JArray)_data["OwnerIDs"]).ToObject<List<ulong>>();
        public string CommandPrefix => (string)_data["CommandPrefix"];
        public string YouTubeKey => (string)_data["YouTubeKey"];

        private JObject _data;

        private JsonConfig() { }

        public static async Task<JsonConfig> Create()
        {
            var config = new JsonConfig();
            await config.Load();

            return config;
        }

        private async Task Load()
        {
            using (var reader = File.OpenText(FilePath))
            {
                var content = await reader.ReadToEndAsync();

                _data = JObject.Parse(content);
            }
        }
    }
}
