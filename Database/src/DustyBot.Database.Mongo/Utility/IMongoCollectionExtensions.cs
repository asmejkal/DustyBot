using System;
using System.Linq.Expressions;
using MongoDB.Driver;

namespace DustyBot.Database.Mongo.Utility
{
    public static class IMongoCollectionExtensions
    {
        public static IUpdateOneFluent<TDocument> UpdateOne<TDocument>(this IMongoCollection<TDocument> collection, Expression<Func<TDocument, bool>> filter)
        {
            return collection.UpdateOne(null, filter);
        }

        public static IUpdateOneFluent<TDocument> UpdateOne<TDocument>(this IMongoCollection<TDocument> collection, FilterDefinition<TDocument> filter)
        {
            return collection.UpdateOne(null, filter);
        }

        public static IUpdateOneFluent<TDocument> UpdateOne<TDocument>(this IMongoCollection<TDocument> collection, IClientSessionHandle session, Expression<Func<TDocument, bool>> filter)
        {
            return collection.UpdateOne(session, new ExpressionFilterDefinition<TDocument>(filter));
        }

        public static IUpdateOneFluent<TDocument> UpdateOne<TDocument>(this IMongoCollection<TDocument> collection, IClientSessionHandle session, FilterDefinition<TDocument> filter)
        {
            return new UpdateOneFluent<TDocument>(session, collection, filter, new UpdateOptions());
        }

        public static IFindOneAndUpdateFluent<TDocument, TDocument> FindOneAndUpdate<TDocument>(this IMongoCollection<TDocument> collection, Expression<Func<TDocument, bool>> filter)
        {
            return collection.FindOneAndUpdate(null, filter);
        }

        public static IFindOneAndUpdateFluent<TDocument, TDocument> FindOneAndUpdate<TDocument>(this IMongoCollection<TDocument> collection, FilterDefinition<TDocument> filter)
        {
            return collection.FindOneAndUpdate(null, filter);
        }

        public static IFindOneAndUpdateFluent<TDocument, TDocument> FindOneAndUpdate<TDocument>(this IMongoCollection<TDocument> collection, IClientSessionHandle session, Expression<Func<TDocument, bool>> filter)
        {
            return collection.FindOneAndUpdate(session, new ExpressionFilterDefinition<TDocument>(filter));
        }

        public static IFindOneAndUpdateFluent<TDocument, TDocument> FindOneAndUpdate<TDocument>(this IMongoCollection<TDocument> collection, IClientSessionHandle session, FilterDefinition<TDocument> filter)
        {
            return new FindOneAndUpdateFluent<TDocument, TDocument>(session, collection, filter, null, new FindOneAndUpdateOptions<TDocument>());
        }
    }
}
