using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ubiquiti.Discovery.Abstractions;
using Ubiquiti.Discovery.Options;
using Ubiquiti.Discovery.Services;

namespace Ubiquiti.Discovery.Extensions;

/// <summary>
/// Registers Ubiquiti discovery services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Ubiquiti discovery service with optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddUbiquitiDiscovery(this IServiceCollection services, Action<UbiquitiDiscoveryOptions>? configure = null)
    {
        if (configure is not null)
            services.Configure(configure);
        else
            services.AddOptions<UbiquitiDiscoveryOptions>();

        services.TryAddSingleton<IUbiquitiDiscoveryService, UbiquitiDiscoveryService>();
        return services;
    }

    /// <summary>
    /// Replaces the default discovery service implementation.
    /// </summary>
    /// <typeparam name="TService">The custom service type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddUbiquitiDiscoveryService<TService>(this IServiceCollection services) where TService : class, IUbiquitiDiscoveryService
    {
        services.Replace(ServiceDescriptor.Singleton<IUbiquitiDiscoveryService, TService>());
        return services;
    }
}