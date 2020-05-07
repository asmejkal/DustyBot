using LiteDB;
using System.IO;
using LiteDB.Engine;

namespace DustyBot.Framework.LiteDB
{
    public static class DatabaseHelpers
    {
        public static LiteDatabase CreateOrOpen(string dbPath, string password) => new LiteDatabase($"Filename={dbPath};Password={password}");

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

        public static void FixSequence(string dbPath, string password)
        {
            var sourceConn = new ConnectionString($"Filename={dbPath};Password={password}");

            var tmpPath = sourceConn.Filename + "_tmp";
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);

            using (var source = new LiteDatabase(sourceConn))
            {
                string destConnString = $"Filename={tmpPath};Password={password}";
                using (var destination = new LiteDatabase(destConnString))
                {
                    foreach (var name in source.GetCollectionNames())
                    {
                        var oldCol = source.GetCollection(name);
                        var newCol = destination.GetCollection(name);
                        
                        var items = oldCol.FindAll();
                        var id = 1;
                        foreach (var item in items)
                        {
                            item["_id"] = id++;
                            newCol.Insert(item);
                        }
                    }

                    destination.UserVersion = source.UserVersion;
                    destination.Rebuild();
                }
            }

            File.Delete(sourceConn.Filename);
            File.Move(tmpPath, sourceConn.Filename);
        }
    }
}
