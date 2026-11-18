using Ubiquiti.Discovery.Models;

namespace Ubiquiti.Discovery.Extensions;

/// <summary>
/// Provides display-oriented formatting helpers for <see cref="DeviceInfo"/>.
/// </summary>
public static class DeviceInfoDisplayExtensions
{
    /// <summary>
    /// Formats the uptime as <c>ddd HH:mm:ss</c> or <c>HH:mm:ss</c>.
    /// </summary>
    public static string ToUptimeDisplay(this DeviceInfo device)
    {
        if (device.Uptime == null)
            return "-";

        var t = TimeSpan.FromSeconds(device.Uptime.Value);
        return t.Days > 0
            ? $"{(int)t.TotalDays}d {t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
    }

    /// <summary>
    /// Formats the firmware version as <c>vX.Y.Z (Platform)</c>, extracting the true platform 
    /// (e.g., XW) from the raw string and ignoring build numbers.
    /// </summary>
    public static string ToFirmwareDisplay(this DeviceInfo device)
    {
        var raw = device.Firmware;
        if (string.IsNullOrWhiteSpace(raw))
            return "-";

        var parts = raw.Split('.');
        int versionIndex = Array.FindIndex(parts, p => p.StartsWith('v') || (p.Length > 0 && char.IsDigit(p[0])));

        string version = "-";
        if (versionIndex >= 0)
        {
            var versionParts = parts.Skip(versionIndex).Take(3);
            version = string.Join(".", versionParts).TrimStart('v');
        }

        // Try to get the real platform from the beginning of the string (e.g., XW, XM).
        // If the first part is not a valid platform (e.g., "var934x"), fallback to the Platform property.
        string platform = (parts.Length > 0 && parts[0].Length >= 2 && parts[0].All(char.IsLetter))
            ? parts[0]
            : (device.Platform ?? "-");

        return $"v{version} ({platform})";
    }
}