using Ubiquiti.Discovery.Models;

namespace Ubiquiti.Discovery.Abstractions;

/// <summary>
/// Discovers Ubiquiti devices on the local network via UDP broadcast.
/// </summary>
public interface IUbiquitiDiscoveryService
{
    /// <summary>
    /// Asynchronously discovers Ubiquiti devices on the local network.
    /// Devices are yielded as soon as they respond; the stream completes when
    /// either the idle timeout or the total timeout is reached.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An asynchronous stream of discovered <see cref="DeviceInfo"/> instances.</returns>
    IAsyncEnumerable<DeviceInfo> DiscoverDevicesAsync(CancellationToken cancellationToken = default);
}