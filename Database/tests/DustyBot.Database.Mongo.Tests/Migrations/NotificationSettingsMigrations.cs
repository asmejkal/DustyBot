using DustyBot.Database.Mongo.Collections.Notifications.Migrations;
using DustyBot.Database.Mongo.Tests.Utility;
using MongoDB.Bson;
using Xunit;

namespace DustyBot.Database.Mongo.Tests.Migrations
{
    public class NotificationSettingsMigrations
    {
        [Fact]
        public void Migration1_1_0Test()
        {
            var before = new BsonDocument()
            {
                ["Notifications"] = new BsonArray()
                {
                    new BsonDocument() 
                    {  
                        ["OriginalWord"] = "Test",
                        ["LoweredWord"] = "test",
                        ["User"] = 123,
                        ["TriggerCount"] = 321
                    }
                }
            };

            var after = new BsonDocument()
            {
                ["Notifications"] = new BsonArray()
                {
                    new BsonDocument()
                    {
                        ["User"] = 123,
                        ["TriggerCount"] = 321,
                        ["Keyword"] = "Test"
                    }
                }
            };

            MigrationAssert.MigratesUp<NotificationSettingsMigration_1_1_0>(before, after);
        }
    }
}
