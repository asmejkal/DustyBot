using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.LiteDB
{
    public interface IMigrations
    {
        Migration GetMigration(ushort version);
    }
}
