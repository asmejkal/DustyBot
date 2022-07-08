using System;
using Mongo.Migration.Migrations.Document;
using MongoDB.Bson;

namespace DustyBot.Database.Mongo.Collections.Reactions.Migrations
{
    /// <summary>
    /// Adds support for embed bye messages.
    /// </summary>
    public sealed class ReactionSettingsMigration_2_0_0 : DocumentMigration<ReactionsSettings>
    {
        public ReactionSettingsMigration_2_0_0() 
            : base("2.0.0")
        {
        }

        public override void Up(BsonDocument document)
        {
            document.Remove("IsPublic");
        }

        public override void Down(BsonDocument document)
        {
            throw new NotImplementedException();
        }
    }
}
