using System.Collections.Generic;
using System.Linq;
using Mongo.Migration.Migrations.Document;
using MongoDB.Bson;
using Xunit;

namespace DustyBot.Database.Mongo.Tests.Utility
{
    public static class MigrationAssert
    {
        private class BsonElementEqualityComparer : EqualityComparer<BsonElement>
        {
            public override bool Equals(BsonElement x, BsonElement y)
            {
                if (x.Name != y.Name)
                    return false;

                if (x.Value is BsonDocument first && y.Value is BsonDocument second)
                    return new BsonDocumentEqualityComparer().Equals(first, second);

                return x.Value.Equals(y.Value);
            }

            public override int GetHashCode(BsonElement obj) => obj.GetHashCode();
        }

        private class BsonDocumentEqualityComparer : EqualityComparer<BsonDocument>
        {
            public override bool Equals(BsonDocument? x, BsonDocument? y)
            {
                if (ReferenceEquals(x, y)) 
                    return true;

                if (x == null || y == null)
                    return false;

                return x.Elements.OrderBy(x => x.Name).SequenceEqual(y.Elements.OrderBy(x => x.Name), new BsonElementEqualityComparer());
            }

            public override int GetHashCode(BsonDocument obj) => obj.GetHashCode();
        }

        public static void MigratesUp<T>(BsonDocument before, BsonDocument after)
            where T : IDocumentMigration, new()
        {
            var migration = new T();

            var migrated = (BsonDocument)before.DeepClone();
            migration.Up(migrated);
            Assert.Equal(after, migrated, new BsonDocumentEqualityComparer());
        }

        public static void MigratesDown<T>(BsonDocument before, BsonDocument after)
            where T : IDocumentMigration, new()
        {
            var migration = new T();

            var migrated = (BsonDocument)after.DeepClone();
            migration.Down(migrated);
            Assert.Equal(before, migrated, new BsonDocumentEqualityComparer());
        }

        public static void MigratesUpDown<T>(BsonDocument before, BsonDocument after)
            where T : IDocumentMigration, new()
        {
            MigratesUp<T>(before, after);
            MigratesDown<T>(before, after);
        }
    }
}
