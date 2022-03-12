using System.Linq;
using Mongo.Migration.Migrations.Database;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DustyBot.Database.Mongo.Migrations
{
    public sealed class DatabaseMigration_2_0_0 : DatabaseMigration
    {
        public DatabaseMigration_2_0_0() 
            : base("2.0.0")
        {
        }

        public override void Up(IMongoDatabase db)
        {
            if (db.ListCollectionNames(new ListCollectionNamesOptions() { Filter = new BsonDocument("name", "EventsSettings") }).Any())
                db.RenameCollection("EventsSettings", "GreetByeSettings");

            var mediaSettings = db.GetCollection<BsonDocument>("MediaSettings");
            var youTubeSettings = db.GetCollection<BsonDocument>("YouTubeSettings");
            var daumCafeSettings = db.GetCollection<BsonDocument>("DaumCafeSettings");
            foreach (var setting in mediaSettings.Find(Builders<BsonDocument>.Filter.Empty).ToEnumerable())
            {
                var songs = setting["YouTubeComebacks"];
                foreach (var song in songs.AsBsonArray.OfType<BsonDocument>().Where(x => x.GetValue("Category", BsonNull.Value).IsBsonNull))
                    song["Category"] = "default";

                youTubeSettings.InsertOne(new BsonDocument()
                {
                    ["_id"] = setting["_id"],
                    ["Songs"] = songs
                });

                daumCafeSettings.InsertOne(new BsonDocument()
                {
                    ["_id"] = setting["_id"],
                    ["Feeds"] = setting["DaumCafeFeeds"]
                });
            }

            db.DropCollection("MediaSettings");

            var userCredentials = db.GetCollection<BsonDocument>("UserCredentials");
            var userDaumCafeSettings = db.GetCollection<BsonDocument>("UserDaumCafeSettings");
            foreach (var setting in userCredentials.Find(Builders<BsonDocument>.Filter.Empty).ToEnumerable())
            {
                userDaumCafeSettings.InsertOne(new BsonDocument()
                {
                    ["_id"] = setting["_id"],
                    ["Credentials"] = setting["Credentials"]
                });
            }

            db.DropCollection("UserCredentials");
        }

        public override void Down(IMongoDatabase db)
        {
            db.RenameCollection("GreetByeSettings", "EventsSettings");
        }
    }
}
