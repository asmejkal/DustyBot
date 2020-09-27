using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;

namespace DustyBot.Framework.LiteDB.Utility
{
    public static class LiteDBExtensions
    {
        public static UInt64 AsUInt64(this BsonValue value)
        {
            return unchecked((UInt64)((Int64)(value.RawValue ?? 0L)));
        }
    }
}
