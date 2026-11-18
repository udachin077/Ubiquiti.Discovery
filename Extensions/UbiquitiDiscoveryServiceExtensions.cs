using Ubiquiti.Discovery.Abstractions;
using Ubiquiti.Discovery.Models;

namespace Ubiquiti.Discovery.Extensions;

/// <summary>
/// Convenience extension methods for <see cref="IUbiquitiDiscoveryService"/>.
/// </summary>
public static class UbiquitiDiscoveryServiceExtensions
{
    /// <summary>
    /// Collects all devices discovered on the local network into a single list.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper around
    /// <see cref="IUbiquitiDiscoveryService.DiscoverDevicesAsync"/>.
    /// The call completes when the idle timeout or the total timeout is reached,
    /// or when the cancellation token is triggered.
    /// </remarks>
    /// <param name="service">The discovery service.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of discovered devices.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
    public static async Task<IReadOnlyList<DeviceInfo>> DiscoverAllAsync(this IUbiquitiDiscoveryService service, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(service);

        var result = new List<DeviceInfo>();
        await foreach (var device in service.DiscoverDevicesAsync(ct).ConfigureAwait(false))
            result.Add(device);

        return result;
    }
}