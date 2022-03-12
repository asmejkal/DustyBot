using System;
using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Mongo.Collections
{
    public sealed class UserDaumCafeSettings : BaseUserSettings, IDisposable
    {
        public List<DaumCafeCredential> Credentials { get; set; } = new();

        public void Dispose()
        {
            foreach (var c in Credentials)
                c.Dispose();

            Credentials.Clear();
        }
    }
}
