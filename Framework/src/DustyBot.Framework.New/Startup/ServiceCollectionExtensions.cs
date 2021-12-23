using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace DustyBot.Framework.Startup
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDiscordModule<T>(this IServiceCollection services)
        {
            var collection = (ModuleCollection?)services.FirstOrDefault(x => x.ServiceType == typeof(ModuleCollection))?.ImplementationInstance;
            if (collection == null)
                services.AddSingleton(collection = new ModuleCollection());

            collection.Add(typeof(T));
            return services;
        }
    }
}
