using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;

namespace DustyBot.Framework.LiteDB
{
    public interface ILiteDbMigrator
    {
        void MigrateCurrent(LiteDatabase db);
        void Migrate(LiteDatabase db, ushort version);
    }
}
