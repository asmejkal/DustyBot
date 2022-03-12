using System;
using DustyBot.Database.Mongo.Utility;
using Mongo.Migration.Migrations.Document;
using MongoDB.Bson;

namespace DustyBot.Database.Mongo.Collections.GreetBye.Migrations
{
    /// <summary>
    /// Adds support for embed bye messages.
    /// </summary>
    public sealed class GreetByeSettingsMigration_2_0_0 : DocumentMigration<GreetByeSettings>
    {
        public GreetByeSettingsMigration_2_0_0() 
            : base("2.0.0")
        {
        }

        public override void Up(BsonDocument document)
        {
            document["Events"] = new BsonArray();
            if (document.TryGetValue("GreetChannel", out var greetChannel))
            {
                var greetSetting = new BsonDocument()
                {
                    ["ChannelId"] = greetChannel
                };

                if (document.TryGetValue("GreetMessage", out var greetMessage))
                {
                    greetSetting["Text"] = greetMessage;
                }
                else if (document.TryGetValue("GreetEmbed", out var greetEmbed))
                {
                    greetSetting["Embed"] = greetEmbed;
                }

                document["Events"].AsBsonArray.Add(new BsonArrayOfDocumentsDictionaryItem("Greet", greetSetting));
            }

            document.Remove("GreetChannel");
            document.Remove("GreetEmbed");
            document.Remove("GreetMessage");

            if (document.TryGetValue("ByeChannel", out var byeChannel)
                && document.TryGetValue("ByeMessage", out var byeMessage)
                && !string.IsNullOrEmpty(byeMessage.ToString()))
            {
                document["Events"].AsBsonArray.Add(new BsonArrayOfDocumentsDictionaryItem("Bye", new BsonDocument()
                {
                    ["ChannelId"] = byeChannel,
                    ["Text"] = byeMessage
                }));
            }

            document.Remove("ByeChannel");
            document.Remove("ByeMessage");
        }

        public override void Down(BsonDocument document)
        {
            throw new NotImplementedException();
        }
    }
}
