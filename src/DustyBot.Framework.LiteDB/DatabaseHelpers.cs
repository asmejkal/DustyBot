using LiteDB;
using System.IO;
using LiteDB.Engine;
using DustyBot.Framework.LiteDB.Utility;
using System;
using System.Linq;

namespace DustyBot.Framework.LiteDB
{
    public static class DatabaseHelpers
    {
        public static LiteDatabase CreateOrOpen(string dbPath, string password) => new LiteDatabase($"Filename={dbPath};Password={password};Upgrade=true;Collation=en-US/IgnoreCase");

        public static void Encrypt(string dbPath, string password)
        {
            using (var source = new LiteDatabase($"Filename={dbPath};Upgrade=true;Collation=en-US/IgnoreCase"))
            {
                source.Rebuild(new RebuildOptions() { Collation = new Collation("en-US/IgnoreCase"), Password = password });
            }
        }

        public static void Decrypt(string dbPath, string password)
        {
            using (var source = new LiteDatabase($"Filename={dbPath};Upgrade=true;Collation=en-US/IgnoreCase;Password={password}"))
            {
                source.Rebuild(new RebuildOptions() { Collation = new Collation("en-US/IgnoreCase")});
            }
        }

        public static void Upgrade(string dbPath, string password)
        {
            var sourceConn = new ConnectionString($"Filename={dbPath};Password={password};Upgrade=true;Collation=en-US/IgnoreCase");

            var tmpPath = sourceConn.Filename + "_tmp";
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);

            using (var source = new LiteDatabase(sourceConn))
            {
                string destConnString = $"Filename={tmpPath};Password={password};Collation=en-US/IgnoreCase";
                using (var destination = new LiteDatabase(destConnString))
                {
                    foreach (var name in source.GetCollectionNames())
                    {
                        var oldCol = source.GetCollection(name);
                        var newCol = destination.GetCollection(name);
                        
                        var items = oldCol.FindAll();
                        foreach (var item in items)
                        {
                            if (item.ContainsKey("ServerId"))
                            {
                                var id = item["ServerId"].AsUInt64();
                                item["_id"] = unchecked((long)id);
                                item.Remove("ServerId");
                            }
                            else if (item.ContainsKey("UserId"))
                            {
                                var id = item["UserId"].AsUInt64();
                                item["_id"] = unchecked((long)id);
                                item.Remove("UserId");
                            }
                            else
                            {
                                if (item["_id"].AsInt32 != 1)
                                    throw new InvalidDataException("Encountered global settings with an ID larger than 1.");
                            }

                            newCol.Insert(item);
                        }
                    }

                    destination.UserVersion = source.UserVersion;
                    destination.Checkpoint();
                    destination.Rebuild();
                    destination.Checkpoint();
                }
            }

            File.Delete(sourceConn.Filename);
            File.Move(tmpPath, sourceConn.Filename);
        }
    }
}
