using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DustyBot.Core.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHostedApiService<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService, IHostedService
        {
            services.AddSingleton<TImplementation>();
            services.AddSingleton<TService>(x => x.GetService<TImplementation>());
            services.AddSingleton<IHostedService>(x => x.GetService<TImplementation>());
            return services;
        }

        public static IServiceCollection AddHostedApiService<TService, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> implementationFactory)
            where TService : class
            where TImplementation : class, TService, IHostedService
        {
            services.AddSingleton(x => implementationFactory(x));
            services.AddSingleton<TService>(x => x.GetService<TImplementation>());
            services.AddSingleton<IHostedService>(x => x.GetService<TImplementation>());
            return services;
        }
    }
}
