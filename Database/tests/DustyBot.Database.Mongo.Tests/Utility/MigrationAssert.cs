using Mongo.Migration.Migrations.Document;
using MongoDB.Bson;
using Xunit;

namespace DustyBot.Database.Mongo.Tests.Utility
{
    public static class MigrationAssert
    {
        public static void MigratesUp<T>(BsonDocument before, BsonDocument after)
            where T : IDocumentMigration, new()
        {
            var migration = new T();

            var migrated = (BsonDocument)before.DeepClone();
            migration.Up(migrated);
            Assert.Equal(after, migrated);
        }

        public static void MigratesDown<T>(BsonDocument before, BsonDocument after)
            where T : IDocumentMigration, new()
        {
            var migration = new T();

            var migrated = (BsonDocument)after.DeepClone();
            migration.Down(migrated);
            Assert.Equal(before, migrated);
        }

        public static void MigratesUpDown<T>(BsonDocument before, BsonDocument after)
            where T : IDocumentMigration, new()
        {
            MigratesUp<T>(before, after);
            MigratesDown<T>(before, after);
        }
    }
}
