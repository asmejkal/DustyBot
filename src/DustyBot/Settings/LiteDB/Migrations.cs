using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using DustyBot.Framework.LiteDB;
using DustyBot.Framework.LiteDB.Utility;
using DustyBot.Framework.Utility;

namespace DustyBot.Settings.LiteDB
{
    public class Migrations : IMigrations
    {
        private SortedSet<Migration> _migrations = new SortedSet<Migration>();
        
        public Migrations()
        {
            _migrations = new SortedSet<Migration>()
            {
                new Migration
                (
                    version: 0
                ),

                new Migration
                (
                    version: 1
                ),

                new Migration
                (
                    version: 2,
                    up: db =>
                    {
                        //Change DaumCafeFeeds/LastPostId to signed integer
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

                new Migration
                (
                    version: 3,
                    up: db =>
                    {
                        //Change DaumCafeFeeds/LastPostId to signed integer
                        var col = db.GetCollection("MediaSettings");
                        foreach (var settings in col.FindAll())
                        {
                            var channelId = settings["ScheduleChannel"].AsUInt64();
                            if (channelId != 0 && settings["ScheduleMessages"].AsArray != null)
                            {
                                var messages = settings["ScheduleMessages"].AsArray;
                                var messageLocs = new List<BsonValue>();
                                foreach (var message in messages)
                                    messageLocs.Add(BsonMapper.Global.ToDocument(new MessageLocation() { MessageId = message.AsUInt64(), ChannelId = channelId }));

                                messages.Clear();
                                messages.AddRange(messageLocs);
                            }

                            settings.Remove("ScheduleChannel");
                            col.Update(settings);
                        }
                    }
                )
            };
        }

        public Migration GetMigration(ushort version)
        {
            var result = _migrations.ElementAtOrDefault(version);
            if (result == null)
                throw new MigrationException($"Missing migration procedure for version {version}");

            return result;
        }
    }
}
