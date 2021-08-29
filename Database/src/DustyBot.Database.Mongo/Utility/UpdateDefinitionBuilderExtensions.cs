using System;
using System.Linq.Expressions;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace DustyBot.Database.Mongo.Utility
{
    public static class UpdateDefinitionBuilderExtensions
    {
        public static UpdateDefinition<TDocument> Toggle<TDocument>(this UpdateDefinitionBuilder<TDocument> builder, FieldDefinition<TDocument, bool> field)
        {
            var serializer = BsonSerializer.LookupSerializer<TDocument>();
            var renderedField = field.Render(serializer, BsonSerializer.SerializerRegistry);
            var pipeline = new EmptyPipelineDefinition<TDocument>()
                .AppendStage("{$set:{" + renderedField + ":{$eq:[false,\"" + renderedField + "\"]}}}", serializer);

            return Builders<TDocument>.Update.Pipeline(pipeline);
        }

        public static UpdateDefinition<TDocument> Toggle<TDocument>(this UpdateDefinitionBuilder<TDocument> builder, Expression<Func<TDocument, bool>> field)
        {
            return builder.Toggle(new ExpressionFieldDefinition<TDocument, bool>(field));
        }
    }
}
