using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.LiteDB
{
    public static class DatabaseHelpers
    {
        public static LiteDatabase CreateOrOpen(string dbPath, string password) => new LiteDatabase($"Filename={dbPath};Password={password}");

        public static void Encrypt(string dbPath, string password)
        {
            CopyReplace(dbPath, password);
        }

        public static void Decrypt(string dbPath, string password)
        {
            CopyReplace($"Filename={dbPath};Password={password}");
        }

        public static void CopyReplace(string sourceConnString, string destPassword = null)
        {
            var sourceConn = new ConnectionString(sourceConnString);

            var tmpPath = sourceConn.Filename + "_tmp";
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);

            using (var source = new LiteDatabase(sourceConnString))
            {
                string destConnString = $"Filename={tmpPath}" + (destPassword == null ? "" : $";Password={destPassword}");
                using (var destination = new LiteDatabase(destConnString))
                {
                    foreach (var name in source.GetCollectionNames())
                    {
                        var oldCol = source.GetCollection(name);
                        var newCol = destination.GetCollection(name);
                        newCol.InsertBulk(oldCol.FindAll(), Math.Max(oldCol.Count(), 100));
                    }

                    destination.Engine.UserVersion = source.Engine.UserVersion;
                }
            }

            File.Delete(sourceConn.Filename);
            File.Move(tmpPath, sourceConn.Filename);
        }
    }
}
