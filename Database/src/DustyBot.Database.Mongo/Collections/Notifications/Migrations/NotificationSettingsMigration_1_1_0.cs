using Mongo.Migration.Migrations.Document;
using MongoDB.Bson;

namespace DustyBot.Database.Mongo.Collections.Notifications.Migrations
{
    /// <summary>
    /// Adds support for embed bye messages.
    /// </summary>
    public sealed class NotificationSettingsMigration_1_1_0 : DocumentMigration<NotificationSettings>
    {
        public NotificationSettingsMigration_1_1_0() 
            : base("1.1.0")
        {
        }

        public override void Up(BsonDocument document)
        {
            foreach (var notification in document["Notifications"].AsBsonArray)
            {
                notification["Keyword"] = notification["OriginalWord"];
                notification.AsBsonDocument.Remove("OriginalWord");
                notification.AsBsonDocument.Remove("LoweredWord");
            }
        }

        public override void Down(BsonDocument document)
        {
            foreach (var notification in document["Notifications"].AsBsonArray)
            {
                notification["OriginalWord"] = notification["Keyword"];
                notification["LoweredWord"] = notification["Keyword"].AsString.ToLowerInvariant();
                notification.AsBsonDocument.Remove("Keyword");
            }
        }
    }
}
