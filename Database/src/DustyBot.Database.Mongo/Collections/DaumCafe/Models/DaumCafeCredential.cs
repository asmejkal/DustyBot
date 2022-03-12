using System;
using System.Security;

namespace DustyBot.Database.Mongo.Models
{
    public sealed class DaumCafeCredential : IDisposable
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
        public string Login { get; set; }
        public SecureString Password { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public DaumCafeCredential()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        public DaumCafeCredential(string name, string login, SecureString password)
        {
            if (password.Length <= 0)
                throw new ArgumentException("Password empty", nameof(password));

            Id = Guid.NewGuid();
            Name = string.IsNullOrEmpty(name) ? throw new ArgumentException("Null or empty", nameof(name)) : name;
            Login = string.IsNullOrEmpty(login) ? throw new ArgumentException("Null or empty", nameof(login)) : login;
            Password = password ?? throw new ArgumentNullException(nameof(password));
        }

        public void Dispose()
        {
            Password?.Dispose();
        }
    }
}
