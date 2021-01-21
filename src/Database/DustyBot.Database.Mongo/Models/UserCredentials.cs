using System;
using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;

namespace DustyBot.Database.Mongo.Models
{
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
