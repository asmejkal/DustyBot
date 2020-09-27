using DustyBot.Database.Mongo.Models;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public static class GoogleHelpers
    {
        public static async Task<GoogleAccountCredentials> ParseServiceAccountKeyFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(stream))
            {
                var data = await reader.ReadToEndAsync();
                var o = JObject.Parse(data);

                return new GoogleAccountCredentials()
                {
                    Email = (string)o["client_email"],
                    Id = (string)o["client_id"],
                    Key = (string)o["private_key"],
                };
            }
        }
    }
}
