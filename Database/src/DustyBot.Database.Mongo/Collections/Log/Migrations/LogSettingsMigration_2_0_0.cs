using System;
using Mongo.Migration.Migrations.Document;
using MongoDB.Bson;

namespace DustyBot.Database.Mongo.Collections.Log.Migrations
{
    /// <summary>
    /// Adds support for embed bye messages.
    /// </summary>
    public abstract class LogSettingsMigration_2_0_0 : DocumentMigration<LogSettings> // TODO
    {
        public LogSettingsMigration_2_0_0() 
            : base("2.0.0")
        {
        }

        public override void Up(BsonDocument document)
        {
            document.Remove("EventMessageDeletedFilter");
            document.Remove("EventNameChangedChannel");

            document["MessageDeletedChannelFilters"] = document["EventMessageDeletedChannelFilter"];
            document.Remove("EventMessageDeletedChannelFilter");

            document["MessageDeletedChannel"] = document["EventMessageDeletedChannel"];
            document.Remove("EventMessageDeletedChannel");
        }

        public override void Down(BsonDocument document)
        {
            throw new NotImplementedException();
        }
    }
}
