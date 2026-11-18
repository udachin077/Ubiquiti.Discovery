namespace Ubiquiti.Discovery.Models;

/// <summary>
/// Represents a MAC + IP pair received in a discovery response.
/// </summary>
public sealed class IpInfo
{
    /// <summary>Gets or sets the MAC address.</summary>
    public string Mac { get; set; }

    /// <summary>Gets or sets the IP address.</summary>
    public string Ip { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IpInfo"/> class.
    /// </summary>
    /// <param name="mac">The MAC address.</param>
    /// <param name="ip">The IP address.</param>
    public IpInfo(string mac, string ip)
    {
        Mac = mac;
        Ip = ip;
    }

    /// <inheritdoc />
    public override string ToString() => $"IpInfo{{mac='{Mac}', ip='{Ip}'}}";
}