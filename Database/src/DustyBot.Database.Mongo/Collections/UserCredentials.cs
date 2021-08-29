using System;
using System.Collections.Generic;
using DustyBot.Database.Mongo.Collections.Templates;
using DustyBot.Database.Mongo.Models;

namespace DustyBot.Database.Mongo.Collections
{
    public sealed class UserCredentials : BaseUserSettings, IDisposable
    {
        public List<Credentials> Credentials { get; set; } = new List<Credentials>();

        public void Dispose()
        {
            foreach (var c in Credentials)
                c.Dispose();

            Credentials.Clear();
        }
    }
}
