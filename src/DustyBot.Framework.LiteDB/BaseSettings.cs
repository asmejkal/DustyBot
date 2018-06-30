using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;

namespace DustyBot.Framework.LiteDB
{
    public abstract class BaseSettings
    {
        [BsonId]
        public int Id { get; set; }
    }
}
