using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace DustyBot.Database.Mongo.Utility
{
    /// <summary>
    /// Fluent interface for findOneAndUpdate.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    /// <typeparam name="TProjection">The type of the projection (same as TDocument if there is no projection).</typeparam>
    public interface IFindOneAndUpdateFluent<TDocument, TProjection>
    {
        FilterDefinition<TDocument> Filter { get; }
        FindOneAndUpdateOptions<TDocument, TProjection> Options { get; }

        IFindOneAndUpdateFluent<TDocument, TProjection> Upsert();
        IFindOneAndUpdateFluent<TDocument, TProjection> ReturnNew();
        IFindOneAndUpdateFluent<TDocument, TNewProjection> Project<TNewProjection>(ProjectionDefinition<TDocument, TNewProjection> projection);
        IFindOneAndUpdateFluent<TDocument, TNewProjection> Project<TNewProjection>(Expression<Func<TDocument, TNewProjection>> projection);

        IFindOneAndUpdateFluent<TDocument, TProjection> WithOption(Action<FindOneAndUpdateOptions<TDocument, TProjection>> action);

        IFindOneAndUpdateFluent<TDocument, TProjection> With(Func<UpdateDefinitionBuilder<TDocument>, UpdateDefinition<TDocument>> builder);

        Task<TProjection> ExecuteAsync(CancellationToken ct = default);
    }
}
