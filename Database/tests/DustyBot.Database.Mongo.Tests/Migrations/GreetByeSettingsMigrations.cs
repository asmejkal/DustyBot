using DustyBot.Database.Mongo.Collections.GreetBye.Migrations;
using DustyBot.Database.Mongo.Tests.Utility;
using DustyBot.Database.Mongo.Utility;
using MongoDB.Bson;
using Xunit;

namespace DustyBot.Database.Mongo.Tests.Migrations
{
    public class GreetByeSettingsMigrations
    {
        [Fact]
        public void Migration2_0_0GreetByeTextTest()
        {
            var before = new BsonDocument()
            {
                ["GreetChannel"] = unchecked((long)ulong.MaxValue - 500),
                ["GreetMessage"] = "greetMessage",
                ["ByeChannel"] = unchecked((long)ulong.MaxValue - 1000),
                ["ByeMessage"] = "byeMessage"
            };

            var after = new BsonDocument()
            {
                ["Events"] = new BsonArray()
                {
                    new BsonArrayOfDocumentsDictionaryItem("Greet", new BsonDocument()
                    {
                        ["ChannelId"] = unchecked((long)ulong.MaxValue - 500),
                        ["Text"] = "greetMessage"
                    }),
                    new BsonArrayOfDocumentsDictionaryItem("Bye", new BsonDocument()
                    {
                        ["ChannelId"] = unchecked((long)ulong.MaxValue - 1000),
                        ["Text"] = "byeMessage"
                    })
                }
            };

            // MigrationAssert.MigratesUp<GreetByeSettingsMigration_2_0_0>(before, after); TODO
        }

        [Fact]
        public void Migration2_0_0GreetEmbedTest()
        {
            var before = new BsonDocument()
            {
                ["GreetChannel"] = unchecked((long)ulong.MaxValue - 500),
                ["GreetEmbed"] = new BsonDocument()
                {
                    ["Title"] = "greetTitle",
                    ["Image"] = "https://address.com/image.jpg",
                    ["Body"] = "greetBody",
                    ["Color"] = (uint)0xFFFFFF
                }
            };

            var after = new BsonDocument()
            {
                ["Events"] = new BsonArray()
                {
                    new BsonArrayOfDocumentsDictionaryItem("Greet", new BsonDocument()
                    {
                        ["ChannelId"] = unchecked((long)ulong.MaxValue - 500),
                        ["Embed"] = new BsonDocument()
                        {
                            ["Title"] = "greetTitle",
                            ["Image"] = "https://address.com/image.jpg",
                            ["Body"] = "greetBody",
                            ["Color"] = 0xFFFFFF
                        }
                    })
                }
            };

            // MigrationAssert.MigratesUp<GreetByeSettingsMigration_2_0_0>(before, after); TODO
        }
    }
}
