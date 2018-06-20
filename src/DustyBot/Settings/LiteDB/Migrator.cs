using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using DustyBot.Framework.Utility;

namespace DustyBot.Settings.LiteDB
{
    public class MigrationException : Exception
    {
        public MigrationException() { }
        public MigrationException(string message) : base(message) { }
        public MigrationException(string message, Exception inner) : base(message, inner) { }
    }

    public class Migrator : Framework.LiteDB.ILiteDbMigrator
    {
        public const ushort CurrentVersion = 1;

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
                if (db.Engine.UserVersion != Version - 1)
                    throw new InvalidOperationException($"Cannot migrate up to version {Version} from version {db.Engine.UserVersion}.");

                _up?.Invoke(db);
                db.Engine.UserVersion = Version;
            }

            public void MigrateDown(LiteDatabase db)
            {
                if (db.Engine.UserVersion != Version + 1)
                    throw new InvalidOperationException($"Cannot migrate down to version {Version} from version {db.Engine.UserVersion}.");

                _down?.Invoke(db);
                db.Engine.UserVersion = Version;
            }
            
            public int CompareTo(Migration obj) => Version.CompareTo(obj.Version);
        }

        private SortedSet<Migration> _migrations;

        public Migrator()
        {
            _migrations = new SortedSet<Migration>()
            {
                new Migration
                (
                    version: 0
                ),

                new Migration
                (
                    version: 1,
                    up: db =>
                    {
                        var col = db.GetCollection("MediaSettings");
                        foreach (var settings in col.FindAll())
                        {
                            settings["DaumCafeFeeds"].AsArray?.ForEach(doc =>
                            {
                                doc.AsDocument["LastPostId"] = Convert.ToInt32(doc.AsDocument["LastPostId"].AsInt64);
                            });

                            col.Update(settings);
                        }
                    }
                ),
            };
        }

        public void MigrateCurrent(LiteDatabase db) => Migrate(db, CurrentVersion);

        public void Migrate(LiteDatabase db, ushort version)
        {
            var dbVersion = db.Engine.UserVersion;
            if (dbVersion == CurrentVersion)
                return;

            if (dbVersion > CurrentVersion + 1)
                throw new MigrationException("The database is more than one version ahead => some downward migration steps are unknown.");

            try
            {
                while (dbVersion < CurrentVersion)
                    _migrations.ElementAt(++dbVersion).MigrateUp(db);

                while (dbVersion > CurrentVersion)
                    _migrations.ElementAt(--dbVersion).MigrateDown(db);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new MigrationException($"Missing migration procedure for version {ex.ActualValue}", ex);
            }
        }
    }
}
