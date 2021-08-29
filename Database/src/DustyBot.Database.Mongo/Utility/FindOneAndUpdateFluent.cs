using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace DustyBot.Database.Mongo.Utility
{
    public class FindOneAndUpdateFluent<TDocument, TProjection> : IFindOneAndUpdateFluent<TDocument, TProjection>
    {
        public FilterDefinition<TDocument> Filter { get; }
        public FindOneAndUpdateOptions<TDocument, TProjection> Options { get; }

        private readonly IClientSessionHandle _session;
        private readonly IMongoCollection<TDocument> _collection;

        private UpdateDefinition<TDocument> _definition;

        public FindOneAndUpdateFluent(IClientSessionHandle session, IMongoCollection<TDocument> collection, FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> definition, FindOneAndUpdateOptions<TDocument, TProjection> options)
        {
            _session = session;
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            Filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _definition = definition;
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IFindOneAndUpdateFluent<TDocument, TNewProjection> Project<TNewProjection>(ProjectionDefinition<TDocument, TNewProjection> projection)
        {
            var newOptions = new FindOneAndUpdateOptions<TDocument, TNewProjection>()
            {
                ArrayFilters = Options.ArrayFilters,
                BypassDocumentValidation = Options.BypassDocumentValidation,
                Collation = Options.Collation,
                Hint = Options.Hint,
                IsUpsert = Options.IsUpsert,
                MaxTime = Options.MaxTime,
                Projection = projection,
                ReturnDocument = Options.ReturnDocument,
                Sort = Options.Sort
            };

            return new FindOneAndUpdateFluent<TDocument, TNewProjection>(_session, _collection, Filter, _definition, newOptions);
        }

        public IFindOneAndUpdateFluent<TDocument, TNewProjection> Project<TNewProjection>(Expression<Func<TDocument, TNewProjection>> projection)
        {
            return Project(new FindExpressionProjectionDefinition<TDocument, TNewProjection>(projection));
        }

        public IFindOneAndUpdateFluent<TDocument, TProjection> ReturnNew()
        {
            Options.ReturnDocument = ReturnDocument.After;
            return this;
        }

        public Task<TProjection> ExecuteAsync(CancellationToken ct = default)
        {
            if (_session != null)
                return _collection.FindOneAndUpdateAsync(_session, Filter, _definition, Options, ct);
            else
                return _collection.FindOneAndUpdateAsync(Filter, _definition, Options, ct);
        }

        public IFindOneAndUpdateFluent<TDocument, TProjection> Upsert()
        {
            Options.IsUpsert = true;
            return this;
        }

        public IFindOneAndUpdateFluent<TDocument, TProjection> With(Func<UpdateDefinitionBuilder<TDocument>, UpdateDefinition<TDocument>> builder)
        {
            _definition = builder(Builders<TDocument>.Update);
            return this;
        }

        public IFindOneAndUpdateFluent<TDocument, TProjection> WithOption(Action<FindOneAndUpdateOptions<TDocument, TProjection>> action)
        {
            action(Options);
            return this;
        }
    }
}
