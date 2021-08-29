using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace DustyBot.Database.Mongo.Utility
{
    public class UpdateOneFluent<TDocument> : IUpdateOneFluent<TDocument>
    {
        public FilterDefinition<TDocument> Filter { get; }
        public UpdateOptions Options { get; }

        private readonly IClientSessionHandle _session;
        private readonly IMongoCollection<TDocument> _collection;

        private UpdateDefinition<TDocument> _definition;

        public UpdateOneFluent(IClientSessionHandle session, IMongoCollection<TDocument> collection, FilterDefinition<TDocument> filter, UpdateOptions options)
        {
            _session = session;
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            Filter = filter ?? throw new ArgumentNullException(nameof(filter));
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Task<UpdateResult> ExecuteAsync(CancellationToken ct = default)
        {
            if (_definition == null)
                throw new InvalidOperationException("Update definition not specified");

            if (_session != null)
                return _collection.UpdateOneAsync(_session, Filter, _definition, Options, ct);
            else
                return _collection.UpdateOneAsync(Filter, _definition, Options, ct);
        }

        public IUpdateOneFluent<TDocument> Upsert()
        {
            Options.IsUpsert = true;
            return this;
        }

        public IUpdateOneFluent<TDocument> With(Func<UpdateDefinitionBuilder<TDocument>, UpdateDefinition<TDocument>> builder)
        {
            _definition = builder(Builders<TDocument>.Update);
            return this;
        }

        public IUpdateOneFluent<TDocument> WithOption(Action<UpdateOptions> action)
        {
            action(Options);
            return this;
        }
    }
}
