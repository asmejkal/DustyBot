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
    public class Credential : IDisposable
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

        #region IDisposable 

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Password?.Dispose();
                    Password = null;
                }

                _disposed = true;
            }
        }

        //~()
        //{
        //    Dispose(false);
        //}

        #endregion
    }

    public class UserCredentials : BaseUserSettings
    {
        public List<Credential> Credentials { get; set; } = new List<Credential>();
        
        #region IDisposable 

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (Credentials != null)
                    {
                        foreach (var c in Credentials)
                            c.Dispose();

                        Credentials.Clear();
                    }                    
                }

                _disposed = true;
            }
        }

        //~()
        //{
        //    Dispose(false);
        //}

        #endregion
    }
}
