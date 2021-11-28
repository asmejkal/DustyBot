using System;
using DustyBot.Database.TableStorage.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DustyBot.Database.TableStorage
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTableStorage(this IServiceCollection services,
            Action<OptionsBuilder<TableStorageOptions>>? configure = null)
        {
            configure?.Invoke(services.AddOptions<TableStorageOptions>());

            return services;
        }
    }
}
