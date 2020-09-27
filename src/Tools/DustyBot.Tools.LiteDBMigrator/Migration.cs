using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.LiteDB
{
    public class Migration : IComparable<Migration>
    {
        public delegate void MigrateDelegate(LiteDatabase db);

        public Migration(ushort version, MigrateDelegate up = null, MigrateDelegate down = null)
        {
            Version = version;
            _up = up;
            _down = down;
        }

        public ushort Version { get; private set; }
        private MigrateDelegate _up;
        private MigrateDelegate _down;

        public void MigrateUp(LiteDatabase db)
        {
            if (db.UserVersion != Version - 1)
                throw new InvalidOperationException($"Cannot migrate up to version {Version} from version {db.UserVersion}.");

            _up?.Invoke(db);
            db.UserVersion = Version;
        }

        public void MigrateDown(LiteDatabase db)
        {
            if (db.UserVersion != Version + 1)
                throw new InvalidOperationException($"Cannot migrate down to version {Version} from version {db.UserVersion}.");

            _down?.Invoke(db);
            db.UserVersion = Version;
        }

        public int CompareTo(Migration obj) => Version.CompareTo(obj.Version);
    }
}
