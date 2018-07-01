using System;
using System.Collections.Generic;
using System.Text;
using DustyBot.Framework.Settings;
using DustyBot.Framework.LiteDB;
using LiteDB;
using System.Security;
using DustyBot.Helpers;

namespace DustyBot.Settings
{
    public class Credential
    {
        static Credential()
        {
            BsonMapper.Global.RegisterType<SecureString>
            (
                serialize: (x) => x.ToByteArray(),
                deserialize: (bson) => bson.AsBinary.ToSecureString()
            );
        }

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }
        public string Login { get; set; }
        public SecureString Password { get; set; }
    }

    public class UserCredentials : BaseUserSettings
    {
        public List<Credential> Credentials { get; set; } = new List<Credential>();
    }
}
