using System;
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
            if (document.TryGetValue("GreetChannel", out var greetChannel) && greetChannel.AsInt64 != default)
            {
                var greetSetting = new BsonDocument()
                {
                    ["ChannelId"] = greetChannel
                };

                if (document.TryGetValue("GreetMessage", out var greetMessage) && !greetMessage.IsBsonNull && !string.IsNullOrEmpty(greetMessage.AsString))
                {
                    greetSetting["Text"] = greetMessage;
                }
                else if (document.TryGetValue("GreetEmbed", out var greetEmbed) && !greetEmbed.IsBsonNull)
                {
                    greetSetting["Embed"] = greetEmbed;
                }

                document["Events"].AsBsonArray.Add(new BsonDocument()
                {
                    ["k"] = 0,
                    ["v"] = greetSetting
                });
            }

            document.Remove("GreetChannel");
            document.Remove("GreetEmbed");
            document.Remove("GreetMessage");

            if (document.TryGetValue("ByeChannel", out var byeChannel) && byeChannel.AsInt64 != default
                && document.TryGetValue("ByeMessage", out var byeMessage) && !byeMessage.IsBsonNull && !string.IsNullOrEmpty(byeMessage.AsString))
            {
                document["Events"].AsBsonArray.Add(new BsonDocument()
                {
                    ["k"] = 1,
                    ["v"] = new BsonDocument()
                    {
                        ["ChannelId"] = byeChannel,
                        ["Text"] = byeMessage
                    }
                });
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
