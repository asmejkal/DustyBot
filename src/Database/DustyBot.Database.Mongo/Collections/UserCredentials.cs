using DustyBot.Database.Mongo.Collections.Templates;
using System;
using System.Collections.Generic;
using System.Security;

namespace DustyBot.Settings
{
    public sealed class Credential : IDisposable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; }
        public string Login { get; set; }
        public SecureString Password { get; set; }

        public void Dispose()
        {
            Password?.Dispose();
            Password = null;
        }
    }

    public sealed class UserCredentials : BaseUserSettings, IDisposable
    {
        public List<Credential> Credentials { get; set; } = new List<Credential>();

        public void Dispose()
        {
            foreach (var c in Credentials)
                c.Dispose();

            Credentials.Clear();
        }
    }
}
