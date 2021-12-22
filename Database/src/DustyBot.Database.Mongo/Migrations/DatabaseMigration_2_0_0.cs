using System.Linq;
using Mongo.Migration.Migrations.Database;
using MongoDB.Bson;
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

            var mediaSettings = db.GetCollection<BsonDocument>("MediaSettings");
            var youTubeSettings = db.GetCollection<BsonDocument>("YouTubeSettings");
            var daumCafeSettings = db.GetCollection<BsonDocument>("DaumCafeSettings");
            foreach (var setting in mediaSettings.Find(Builders<BsonDocument>.Filter.Empty).ToEnumerable())
            {
                var songs = setting["YouTubeComebacks"];
                foreach (var song in songs.AsBsonArray.OfType<BsonDocument>().Where(x => x.GetValue("Category", null) == null))
                    song["Category"] = "default";

                youTubeSettings.InsertOne(new BsonDocument()
                {
                    ["ServerId"] = setting["ServerId"],
                    ["Songs"] = songs
                });

                daumCafeSettings.InsertOne(new BsonDocument()
                {
                    ["ServerId"] = setting["ServerId"],
                    ["Feeds"] = setting["DaumCafeFeeds"]
                });
            }

            db.DropCollection("MediaSettings");
        }

        public override void Down(IMongoDatabase db)
        {
            db.RenameCollection("GreetByeSettings", "EventsSettings");
        }
    }
}
