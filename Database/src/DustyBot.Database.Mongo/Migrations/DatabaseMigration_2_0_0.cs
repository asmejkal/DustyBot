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
                foreach (var song in songs.AsBsonArray.OfType<BsonDocument>().Where(x => !x.TryGetValue("Category", out var value) || value.IsBsonNull || string.IsNullOrEmpty(value.AsString)))
                    song["Category"] = "default";

                if (songs.AsBsonArray.Any())
                {
                    youTubeSettings.InsertOne(new BsonDocument()
                    {
                        ["_id"] = setting["_id"],
                        ["Songs"] = songs
                    });
                }

                if (setting["DaumCafeFeeds"].AsBsonArray.Any())
                {
                    daumCafeSettings.InsertOne(new BsonDocument()
                    {
                        ["_id"] = setting["_id"],
                        ["Feeds"] = setting["DaumCafeFeeds"]
                    });
                }
            }

            db.DropCollection("MediaSettings");

            if (db.ListCollectionNames(new ListCollectionNamesOptions() { Filter = new BsonDocument("name", "UserCredentials") }).Any())
                db.RenameCollection("UserCredentials", "UserDaumCafeSettings");
        }

        public override void Down(IMongoDatabase db)
        {
            db.RenameCollection("GreetByeSettings", "EventsSettings");
            db.RenameCollection("UserDaumCafeSettings", "UserCredentials");
        }
    }
}
