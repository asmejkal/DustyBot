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
        public static void Encrypt(string dbPath, string password)
        {
            var tmpPath = dbPath + "_tmp";
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);

            using (var original = new LiteDatabase(dbPath))
            using (var encrypted = new LiteDatabase($"Filename={tmpPath};Password={password}"))
            {
                foreach (var name in original.GetCollectionNames())
                {
                    var oldCol = original.GetCollection(name);
                    var newCol = encrypted.GetCollection(name);
                    newCol.InsertBulk(oldCol.FindAll(), Math.Max(oldCol.Count(), 100));
                }
            }

            File.Delete(dbPath);
            File.Move(tmpPath, dbPath);
        }

        public static void Decrypt(string dbPath, string password)
        {
            var tmpPath = dbPath + "_tmp";
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);

            using (var original = new LiteDatabase($"Filename={dbPath};Password={password}"))
            using (var decrypted = new LiteDatabase(tmpPath))
            {
                foreach (var name in original.GetCollectionNames())
                {
                    var oldCol = original.GetCollection(name);
                    var newCol = decrypted.GetCollection(name);
                    newCol.InsertBulk(oldCol.FindAll(), Math.Max(oldCol.Count(), 100));
                }
            }

            File.Delete(dbPath);
            File.Move(tmpPath, dbPath);
        }
    }
}
