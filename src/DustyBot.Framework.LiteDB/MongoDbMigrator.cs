using DustyBot.Framework.LiteDB.Utility;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DustyBot.Framework.LiteDB
{
    public static class MongoDbMigrator
    {
        public static void Migrate(string dbPath, string password, string mongoDbConnectionString, string mongoDbDatabase)
        {
            var sourceConn = new ConnectionString($"Filename={dbPath};Password={password};Upgrade=true;Collation=en-US/IgnoreCase");

            using (var source = new LiteDatabase(sourceConn))
            {
                var mongoClient = new MongoDB.Driver.MongoClient(mongoDbConnectionString);
                var mongoDb = mongoClient.GetDatabase(mongoDbDatabase);
                
                foreach (var name in source.GetCollectionNames())
                {
                    var oldCol = source.GetCollection(name);
                    var newCol = mongoDb.GetCollection<MongoDB.Bson.BsonDocument>(name);

                    var items = oldCol.FindAll();
                    foreach (var item in items)
                    {
                        try
                        {
                            void ReplaceType(BsonValue value)
                            {
                                IEnumerable<BsonValue> children = null;
                                if (value.IsDocument)
                                {
                                    var doc = value.AsDocument;
                                    if (doc.ContainsKey("_type"))
                                    {
                                        var type = doc["_type"].AsString;
                                        doc.Remove("_type");
                                        doc["_t"] = type.Split(new[] { '.', ',' }).SkipLast(1).Last();
                                    }

                                    children = doc.Values;
                                }
                                else if (value.IsArray)
                                {
                                    children = value.AsArray;
                                }

                                if (children != null)
                                    foreach (var child in children)
                                        ReplaceType(child);
                            }

                            ReplaceType(item);
                            var json = item.ToString();
                            
                            var doc = MongoDB.Bson.BsonDocument.Parse(json);
                            newCol.InsertOne(doc);
                        }
                        catch (Exception ex)
                        {
                            var s = item.ToString();
                            Console.WriteLine($"Failed to migrate a document in {name} collection ({item["_id"]}): {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}
