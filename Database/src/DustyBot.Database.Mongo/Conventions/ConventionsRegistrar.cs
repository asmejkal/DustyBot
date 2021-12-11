using System;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;

namespace DustyBot.Database.Mongo.Conventions
{
    public static class ConventionsRegistrar
    {
        public static void RegisterDefaults()
        {
            Register(new DictionaryRepresentationConvention(DictionaryRepresentation.ArrayOfDocuments));
        }

        public static void Register<T>(T convention, Func<Type, bool> filter)
            where T : IMemberMapConvention
        {
            ConventionRegistry.Register(nameof(T), new ConventionPack() { convention }, filter);
        }

        public static void Register<T>(T convention)
            where T : IMemberMapConvention
        {
            Register(convention, x => true);
        }
    }
}
