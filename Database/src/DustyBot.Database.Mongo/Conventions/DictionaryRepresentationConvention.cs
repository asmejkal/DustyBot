using System;
using System.Linq;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;

namespace DustyBot.Database.Mongo.Conventions
{
    public class DictionaryRepresentationConvention : ConventionBase, IMemberMapConvention
    {
        private readonly DictionaryRepresentation _dictionaryRepresentation;

        public DictionaryRepresentationConvention(DictionaryRepresentation dictionaryRepresentation = DictionaryRepresentation.ArrayOfDocuments)
        {
            _dictionaryRepresentation = dictionaryRepresentation;
        }

        public void Apply(BsonMemberMap memberMap)
        {
            memberMap.SetSerializer(ConfigureSerializer(memberMap.GetSerializer(), Array.Empty<IBsonSerializer>()));
        }

        private IBsonSerializer ConfigureSerializer(IBsonSerializer serializer, IBsonSerializer[] stack)
        {
            if (serializer is IDictionaryRepresentationConfigurable dictionaryRepresentationConfigurable)
            {
                serializer = dictionaryRepresentationConfigurable.WithDictionaryRepresentation(_dictionaryRepresentation);
            }

            if (serializer is IChildSerializerConfigurable childSerializerConfigurable)
            {
                if (!stack.Contains(childSerializerConfigurable.ChildSerializer))
                {
                    var newStack = stack.Union(new[] { serializer }).ToArray();
                    var childConfigured = ConfigureSerializer(childSerializerConfigurable.ChildSerializer, newStack);
                    return childSerializerConfigurable.WithChildSerializer(childConfigured);
                }
            }

            return serializer;
        }
    }
}
