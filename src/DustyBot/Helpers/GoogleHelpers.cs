using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Helpers
{
    public static class GoogleHelpers
    {
        public class RawServiceAccountCredential
        {
            public string Email { get; set; }
            public string Id { get; set; }
            public string Key { get; set; }
        }

        public static async Task<RawServiceAccountCredential> ParseServiceAccountKeyFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(stream))
            {
                var data = await reader.ReadToEndAsync();
                var o = JObject.Parse(data);

                return new RawServiceAccountCredential()
                {
                    Email = (string)o["client_email"],
                    Id = (string)o["client_id"],
                    Key = (string)o["private_key"],
                };
            }
        }
    }
}
