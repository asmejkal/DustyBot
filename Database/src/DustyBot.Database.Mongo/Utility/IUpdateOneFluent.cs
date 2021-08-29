using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace DustyBot.Database.Mongo.Utility
{
    /// <summary>
    /// Fluent interface for update.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    public interface IUpdateOneFluent<TDocument>
    {
        FilterDefinition<TDocument> Filter { get; }
        UpdateOptions Options { get; }

        IUpdateOneFluent<TDocument> Upsert();
        IUpdateOneFluent<TDocument> WithOption(Action<UpdateOptions> action);

        IUpdateOneFluent<TDocument> With(Func<UpdateDefinitionBuilder<TDocument>, UpdateDefinition<TDocument>> builder);

        Task<UpdateResult> ExecuteAsync(CancellationToken ct = default);
    }
}
