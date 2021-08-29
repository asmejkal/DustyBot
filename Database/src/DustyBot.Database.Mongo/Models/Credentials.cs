using System;
using System.Security;

namespace DustyBot.Database.Mongo.Models
{
    public sealed class Credentials : IDisposable
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
}
