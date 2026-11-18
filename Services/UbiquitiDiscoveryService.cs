using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Ubiquiti.Discovery.Abstractions;
using Ubiquiti.Discovery.Models;
using Ubiquiti.Discovery.Options;

namespace Ubiquiti.Discovery.Services;

/// <summary>
/// Discovers Ubiquiti devices on the local network via UDP broadcast on port 10001.
/// </summary>
public sealed class UbiquitiDiscoveryService : IUbiquitiDiscoveryService
{
    private readonly UbiquitiDiscoveryOptions _options;
    private readonly ILogger<UbiquitiDiscoveryService> _logger;

    private static readonly byte[] RequestPayloadV1 = { 0x01, 0x00, 0x00, 0x00 };
    private static readonly byte[] RequestPayloadV2 = { 0x02, 0x08, 0x00, 0x00 };

    /// <summary>
    /// Initializes a new instance of the <see cref="UbiquitiDiscoveryService"/> class.
    /// </summary>
    public UbiquitiDiscoveryService(IOptions<UbiquitiDiscoveryOptions> options, ILogger<UbiquitiDiscoveryService>? logger = null)
    {
        _options = options?.Value ?? new UbiquitiDiscoveryOptions();
        _logger = logger ?? NullLogger<UbiquitiDiscoveryService>.Instance;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DeviceInfo> DiscoverDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var seenMacs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var interfaces = GetNetworkInterfaces();

        if (interfaces.Count == 0)
        {
            _logger.LogWarning("No usable network interfaces found.");
            yield break;
        }

        var sockets = CreateSockets(interfaces);
        if (sockets.Count == 0)
        {
            _logger.LogWarning("No sockets could be created.");
            yield break;
        }

        try
        {
            var broadcastAddresses = GetBroadcastAddresses(interfaces);
            _logger.LogDebug("Sending discovery requests to {AddressCount} addresses.", broadcastAddresses.Count);

            await SendRequestsAsync(sockets, broadcastAddresses, cancellationToken).ConfigureAwait(false);

            await foreach (var device in CollectResponsesAsync(sockets, seenMacs, cancellationToken).ConfigureAwait(false))
            {
                yield return device;
            }
        }
        finally
        {
            foreach (var sock in sockets)
            {
                try { sock.Close(); } catch { }
                try { sock.Dispose(); } catch { }
            }
            _logger.LogInformation("Discovery completed. Found {Count} devices.", seenMacs.Count);
        }
    }

    // ------------------------------------------------------------
    // Sockets
    // ------------------------------------------------------------
    private List<Socket> CreateSockets(List<(string name, string ip, string broadcast)> interfaces)
    {
        var sockets = new List<Socket>();
        foreach (var (name, ip, _) in interfaces)
        {
            try
            {
                var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                sock.Bind(new IPEndPoint(IPAddress.Parse(ip), 0));
                sock.ReceiveTimeout = 500;
                sockets.Add(sock);
                _logger.LogDebug("Socket created for interface {Interface} ({IP})", name, ip);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create socket for interface {Interface}", name);
            }
        }
        return sockets;
    }

    // ------------------------------------------------------------
    // Sending
    // ------------------------------------------------------------
    private async Task SendRequestsAsync(List<Socket> sockets, List<string> broadcastAddresses, CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(Math.Min(sockets.Count * 2, 16));
        var tasks = new List<Task>();

        foreach (var sock in sockets)
        {
            foreach (var bc in broadcastAddresses)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                tasks.Add(SendWithSemaphore(sock, bc, RequestPayloadV1, semaphore, cancellationToken));
                tasks.Add(SendWithSemaphore(sock, bc, RequestPayloadV2, semaphore, cancellationToken));
            }
        }

        if (tasks.Count == 0) return;

        _logger.LogDebug("Sending {Count} discovery requests...", tasks.Count);
        await Task.WhenAll(tasks).ConfigureAwait(false);
        _logger.LogDebug("All discovery requests sent.");
    }

    private async Task SendWithSemaphore(Socket sock, string broadcastAddress, byte[] payload, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SendRequestAsync(sock, broadcastAddress, payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task SendRequestAsync(Socket sock, string broadcastAddress, byte[] payload, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(broadcastAddress), _options.DiscoveryPort);
            await sock.SendToAsync(
                new ArraySegment<byte>(payload),
                SocketFlags.None,
                endpoint,
                cancellationToken
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Send error to {BroadcastAddress}", broadcastAddress);
        }
    }

    // ------------------------------------------------------------
    // Receiving
    // ------------------------------------------------------------
    private async IAsyncEnumerable<DeviceInfo> CollectResponsesAsync(List<Socket> sockets, HashSet<string> seenMacs, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new byte[2048];
        var idleTimeout = TimeSpan.FromSeconds(_options.IdleTimeoutSeconds);
        var maxTotalTime = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        var startTime = DateTime.Now;
        var lastDeviceTime = DateTime.Now;

        var receiveTasks = new Dictionary<Socket, Task<(int bytes, EndPoint? endpoint)>>();
        foreach (var sock in sockets)
        {
            if (IsSocketUsable(sock))
                receiveTasks[sock] = ReceiveFromSocketAsync(sock, buffer, cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested && (DateTime.Now - startTime) < maxTotalTime)
        {
            if (seenMacs.Count > 0 && (DateTime.Now - lastDeviceTime) > idleTimeout)
            {
                _logger.LogDebug("No new devices for {IdleTimeout}s, stopping discovery.", idleTimeout.TotalSeconds);
                break;
            }

            if (receiveTasks.Count == 0)
                break;

            Task? completedTask = null;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(100));

                completedTask = await Task.WhenAny(receiveTasks.Values)
                    .WaitAsync(cts.Token)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Error in receive loop");
                continue;
            }

            if (completedTask != null)
            {
                var kvp = receiveTasks.FirstOrDefault(p => p.Value == completedTask);
                if (kvp.Key != null)
                {
                    receiveTasks.Remove(kvp.Key);
                    bool success = false;
                    (int bytes, EndPoint? endpoint) result = default;

                    try
                    {
                        result = await ((Task<(int bytes, EndPoint? endpoint)>)completedTask)
                            .ConfigureAwait(false);
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error awaiting receive task");
                    }

                    if (success && result.bytes > 0 && result.endpoint != null)
                    {
                        var data = new byte[result.bytes];
                        Array.Copy(buffer, data, result.bytes);
                        var senderIp = (result.endpoint as IPEndPoint)?.Address?.ToString() ?? "";

                        var device = ParseResponse(data, senderIp);
                        if (device != null)
                        {
                            var mac = device.MacAddress;
                            if (!string.IsNullOrEmpty(mac) && seenMacs.Add(mac))
                            {
                                lastDeviceTime = DateTime.Now;
                                _logger.LogInformation(
                                    "Found device: {IpAddress} | {MacAddress} | {Model}",
                                    device.IpAddress, mac, device.Model);

                                yield return device;
                            }
                        }
                    }

                    if (IsSocketUsable(kvp.Key) && !cancellationToken.IsCancellationRequested)
                    {
                        receiveTasks[kvp.Key] = ReceiveFromSocketAsync(kvp.Key, buffer, cancellationToken);
                    }
                }
            }
        }
    }

    private static bool IsSocketUsable(Socket sock) => sock != null && sock.IsBound;

    private async Task<(int bytes, EndPoint? endpoint)> ReceiveFromSocketAsync(Socket sock, byte[] buffer, CancellationToken cancellationToken)
    {
        try
        {
            var remoteEP = new IPEndPoint(IPAddress.Any, 0) as EndPoint;
            var result = await sock.ReceiveFromAsync(
                new ArraySegment<byte>(buffer),
                SocketFlags.None,
                remoteEP
            ).ConfigureAwait(false);

            return (result.ReceivedBytes, result.RemoteEndPoint);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
        {
            return (0, null);
        }
        catch (ObjectDisposedException)
        {
            return (0, null);
        }
        catch (OperationCanceledException)
        {
            return (0, null);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error in ReceiveFromSocketAsync");
            return (0, null);
        }
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------
    private static List<string> GetBroadcastAddresses(List<(string name, string ip, string broadcast)> interfaces)
    {
        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, _, bc) in interfaces)
            if (!string.IsNullOrEmpty(bc))
                addresses.Add(bc);

        addresses.Add("255.255.255.255");
        addresses.Add("233.89.188.1");
        return addresses.ToList();
    }

    private List<(string name, string ip, string broadcast)> GetNetworkInterfaces()
    {
        var result = new List<(string, string, string)>();
        try
        {
            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (var iface in interfaces)
            {
                if (iface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    continue;

                var ipProps = iface.GetIPProperties();
                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(unicast.Address) &&
                        unicast.IPv4Mask != null)
                    {
                        var broadcast = GetBroadcastAddress(unicast.Address, unicast.IPv4Mask);
                        if (broadcast != null)
                            result.Add((iface.Name, unicast.Address.ToString(), broadcast.ToString()));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting network interfaces");
        }
        return result;
    }

    private static IPAddress? GetBroadcastAddress(IPAddress address, IPAddress mask)
    {
        try
        {
            byte[] ipBytes = address.GetAddressBytes();
            byte[] maskBytes = mask.GetAddressBytes();
            if (ipBytes.Length != 4 || maskBytes.Length != 4)
                return null;

            var broadcast = new byte[4];
            for (int i = 0; i < 4; i++)
                broadcast[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
            return new IPAddress(broadcast);
        }
        catch
        {
            return null;
        }
    }

    private DeviceInfo? ParseResponse(byte[] data, string senderIp)
    {
        try
        {
            if (data.Length < 4) return null;

            int version = data[0];
            int length = (data[2] << 8) | data[3];
            if (data.Length != length + 4) return null;

            var device = new DeviceInfo
            {
                IpAddress = senderIp,
                PacketVersion = version
            };

            int index = 4;
            while (index + 3 < data.Length)
            {
                int type = data[index++];
                int fieldLen = (data[index] << 8) | data[index + 1];
                index += 2;

                if (index + fieldLen > data.Length) break;

                byte[] fieldData = new byte[fieldLen];
                Array.Copy(data, index, fieldData, 0, fieldLen);
                device.Records[type] = ParseField(type, fieldData);
                index += fieldLen;
            }

            return device;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error parsing response from {SenderIp}", senderIp);
            return null;
        }
    }

    private static object ParseField(int type, byte[] data)
    {
        try
        {
            return type switch
            {
                0x01 => ParseMac(data),
                0x02 => ParseIpInfo(data),
                0x03 => ParseString(data),
                0x04 => ParseIpAddress(data),
                0x05 => ParseMac(data),
                0x06 => ParseString(data),
                0x07 => ParseHex(data),
                0x08 => ParseHex(data),
                0x09 => ParseHex(data),
                0x0A => data,
                0x0B => ParseString(data),
                0x0C => ParseString(data),
                0x0D => ParseString(data),
                0x0E => data,
                0x10 => data,
                0x12 => data,
                0x13 => ParseMac(data),
                0x14 => ParseString(data),
                0x15 => ParseString(data),
                0x16 => ParseString(data),
                0x17 => data,
                0x18 => data,
                0x19 => data,
                0x1A => data,
                0x1B => ParseString(data),
                0x1C => data,
                _ => data
            };
        }
        catch
        {
            return data;
        }
    }

    private static string ParseString(byte[] data) => Encoding.UTF8.GetString(data).Trim('\0');
    private static string ParseHex(byte[] data) => BitConverter.ToString(data).Replace("-", "").ToLowerInvariant();
    private static string ParseMac(byte[] data) => BitConverter.ToString(data).Replace('-', ':').ToLowerInvariant();

    private static string ParseIpAddress(byte[] data)
    {
        if (data.Length != 4) return "";
        return $"{data[0]}.{data[1]}.{data[2]}.{data[3]}";
    }

    private static IpInfo ParseIpInfo(byte[] data)
    {
        if (data.Length < 10) return new IpInfo("", "");

        byte[] mac = new byte[6];
        byte[] ip = new byte[4];
        Array.Copy(data, 0, mac, 0, 6);
        Array.Copy(data, 6, ip, 0, 4);

        return new IpInfo(
            BitConverter.ToString(mac).Replace('-', ':').ToLowerInvariant(),
            $"{ip[0]}.{ip[1]}.{ip[2]}.{ip[3]}"
        );
    }
}