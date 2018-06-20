using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using DustyBot.Framework.Utility;

namespace DustyBot.Framework.LiteDB
{
    public class MigrationException : Exception
    {
        public MigrationException() { }
        public MigrationException(string message) : base(message) { }
        public MigrationException(string message, Exception inner) : base(message, inner) { }
    }

    public class Migrator
    {
        public ushort CurrentVersion { get; set; }

        private IMigrations _migrations;

        public Migrator(ushort currentVersion, IMigrations migrations)
        {
            CurrentVersion = currentVersion;
            _migrations = migrations;
        }

        public void MigrateCurrent(LiteDatabase db) => Migrate(db, CurrentVersion);

        public void Migrate(LiteDatabase db, ushort version)
        {
            var dbVersion = db.Engine.UserVersion;
            if (dbVersion == CurrentVersion)
                return;

            if (dbVersion > CurrentVersion + 1)
                throw new MigrationException("The database is more than one version ahead => some downward migration steps are unknown.");
            
            while (dbVersion < CurrentVersion)
                _migrations.GetMigration(++dbVersion).MigrateUp(db);

            while (dbVersion > CurrentVersion)
                _migrations.GetMigration(--dbVersion).MigrateDown(db);
        }
    }
}
