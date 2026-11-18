namespace Ubiquiti.Discovery.Options;

/// <summary>
/// Options for the Ubiquiti discovery service.
/// </summary>
public sealed class UbiquitiDiscoveryOptions
{
    /// <summary>Gets or sets the UDP port used for discovery. Default is 10001.</summary>
    public int DiscoveryPort { get; set; } = 10001;

    /// <summary>Gets or sets the idle timeout in seconds. Default is 1.5.</summary>
    public double IdleTimeoutSeconds { get; set; } = 1.5;

    /// <summary>Gets or sets the total discovery timeout in seconds. Default is 10.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}