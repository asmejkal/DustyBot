using Mongo.Migration.Migrations.Database;
using MongoDB.Driver;

namespace DustyBot.Database.Mongo.Migrations
{
    public class DatabaseMigration_2_0_0 : DatabaseMigration
    {
        public DatabaseMigration_2_0_0() 
            : base("2.0.0")
        {
        }

        public override void Up(IMongoDatabase db)
        {
            db.RenameCollection("EventsSettings", "GreetByeSettings");
        }

        public override void Down(IMongoDatabase db)
        {
            db.RenameCollection("GreetByeSettings", "EventsSettings");
        }
    }
}
