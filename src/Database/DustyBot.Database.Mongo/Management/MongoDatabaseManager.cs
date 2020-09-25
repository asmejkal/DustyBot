using MongoDB.Driver;
using System.Threading.Tasks;

namespace DustyBot.Database.Mongo.Management
{
    public static class MongoDatabaseManager
    {
        public static Task DropDatabaseAsync(string connectionString, string name)
        {
            var client = new MongoClient(connectionString);
            return client.DropDatabaseAsync(name);
        }
    }
}
