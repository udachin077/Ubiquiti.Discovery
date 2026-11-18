using System.Text;

namespace Ubiquiti.Discovery.Models;

/// <summary>
/// Represents a Ubiquiti device discovered via UDP broadcast.
/// </summary>
public sealed class DeviceInfo
{
    /// <summary>Gets or sets the IP address from which the response was received.</summary>
    public string IpAddress { get; set; } = "";

    /// <summary>Gets or sets the discovery protocol version (1 or 2).</summary>
    public int PacketVersion { get; set; }

    /// <summary>Gets or sets the raw records returned by the device, keyed by record type.</summary>
    public Dictionary<int, object> Records { get; set; } = [];

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="DeviceInfo"/> class.
    /// </summary>
    public DeviceInfo()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceInfo"/> class with the supplied fields.
    /// </summary>
    public DeviceInfo(string ipAddress, string macAddress, string? hostname, string? model, string? firmwareVersion)
    {
        IpAddress = ipAddress;
        Records = [];

        if (!string.IsNullOrEmpty(macAddress))
        {
            if (macAddress.Contains(':'))
                Records[0x01] = macAddress;
            else
                Records[0x02] = new IpInfo(macAddress, ipAddress);
        }

        if (!string.IsNullOrEmpty(hostname))
            Records[0x0B] = hostname;

        if (!string.IsNullOrEmpty(model))
            Records[0x14] = model;

        if (!string.IsNullOrEmpty(firmwareVersion))
            Records[0x03] = firmwareVersion;
    }

    /// <summary>Gets the MAC address, if reported.</summary>
    public string? MacAddress => GetString(0x01) ?? GetMacFromIpInfo();

    /// <summary>Gets the hostname, if reported.</summary>
    public string? Hostname => GetString(0x0B);

    /// <summary>Gets the device model, if reported.</summary>
    public string? Model => GetString(0x14) ?? GetString(0x15);

    /// <summary>Gets the platform, if reported.</summary>
    public string? Platform => GetString(0x0C);

    /// <summary>Gets the firmware version, if reported.</summary>
    public string? Firmware => GetString(0x03);

    /// <summary>Gets the short firmware version, if reported.</summary>
    public string? ShortVersion => GetString(0x16);

    /// <summary>Gets the device uptime in seconds, if reported.</summary>
    public long? Uptime => GetNumber(0x0A);

    /// <summary>Gets the web UI port, if reported.</summary>
    public int? WebUiPort => GetWebUiPort();

    /// <summary>Gets the device management status (Pending / Adopted).</summary>
    public string? Status => GetStatus();

    /// <summary>Gets the wireless ESSID, if reported.</summary>
    public string? ESSID => GetString(0x0D);

    private string? GetString(int type)
    {
        if (Records.TryGetValue(type, out var value) && value is string s)
            return s;
        return null;
    }

    private long? GetNumber(int type)
    {
        if (Records.TryGetValue(type, out var value) && value is byte[] bytes)
            return ParseInt(bytes);
        return null;
    }

    private string? GetMacFromIpInfo()
    {
        if (Records.TryGetValue(0x02, out var value) && value is IpInfo info)
            return info.Mac;
        return null;
    }

    private int? GetWebUiPort()
    {
        if (!Records.TryGetValue(0x10, out var value))
            return null;

        if (value is not byte[] bytes)
            return null;

        var num = ParseInt(bytes);

        return PacketVersion == 1
            ? (int)(num & 0xFFFF)
            : (int)((num >> 8) & 0xFF);
    }

    private string? GetStatus()
    {
        if (!Records.TryGetValue(0x17, out var value))
            return "Unknown";

        if (value is byte[] bytes && bytes.Length > 0)
            return bytes[0] == 1 ? "Pending" : "Adopted";

        return "Unknown";
    }

    private static long ParseInt(byte[] bytes)
    {
        long result = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            int shift = ((bytes.Length - 1) - i) * 8;
            result += (long)(bytes[i] & 255) << shift;
        }
        return result;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"============ '{Model ?? "Unknown"}' v0x{PacketVersion:X} ============");

        foreach (var kvp in Records.OrderBy(x => x.Key))
        {
            string typeName = GetTypeName(kvp.Key);
            string value = FormatValue(kvp.Value);
            sb.AppendLine($"  {typeName} (0x{kvp.Key:X2}): {value}");
        }

        return sb.ToString();
    }

    private static string GetTypeName(int type) => type switch
    {
        0x01 => "HW_ADDRESS",
        0x02 => "IPINFO",
        0x03 => "FW_VERSION",
        0x04 => "ADDRESS_ENTRY",
        0x05 => "MAC_ENTRY",
        0x06 => "USERNAME",
        0x07 => "SALT",
        0x08 => "RND_CHALLENGE",
        0x09 => "CHALLENGE",
        0x0A => "UPTIME",
        0x0B => "HOSTNAME",
        0x0C => "PLATFORM",
        0x0D => "ESSID",
        0x0E => "WIFI_MODE",
        0x10 => "WEB_UI",
        0x12 => "SEQ",
        0x13 => "SOURCE_MAC",
        0x14 => "MODEL",
        0x15 => "MODEL_V2",
        0x16 => "SHORT_VERSION",
        0x17 => "DEFAULT",
        0x18 => "LOCATING",
        0x19 => "DHCPC",
        0x1A => "DHCPC_BOUND",
        0x1B => "REQ_W",
        0x1C => "SSHD_PORT",
        _ => "<Unknown>"
    };

    private static string FormatValue(object value)
    {
        if (value is IpInfo info)
            return $"IpInfo{{mac='{info.Mac}', ip='{info.Ip}'}}";

        if (value is byte[] bytes)
        {
            if (bytes.Length == 0)
                return "length=0";

            try
            {
                var str = Encoding.UTF8.GetString(bytes).Trim('\0');
                if (!string.IsNullOrEmpty(str) && str.All(c => !char.IsControl(c)))
                    return str;
            }
            catch { }

            return BitConverter.ToString(bytes).Replace("-", "");
        }

        return value?.ToString() ?? "null";
    }
}