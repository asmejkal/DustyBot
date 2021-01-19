namespace DustyBot.Database.Services.Configuration
{
    public class DatabaseOptions
    {
        public string MongoDbConnectionString { get; set; }
        public string SqlDbConnectionString { get; set; }
        public string TableStorageConnectionString { get; set; }
    }
}
