using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DustyBot.Core.Json
{
    public static class JTokenExtensions
    {
        public static JToken RequiredValue(this JToken x, string propertyName) =>
            x[propertyName] ?? throw new JsonSerializationException($"Required property '{propertyName}' expects a value but got null.");

        public static T RequiredValue<T>(this JToken x, string propertyName) =>
            x.Value<T>(propertyName) ?? throw new JsonSerializationException($"Required property '{propertyName}' is null or not of type {typeof(T).FullName}.");
    }
}
