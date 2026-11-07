namespace Jellyfin.Plugin.HyperionGrabber.Configuration;

/// <summary>
/// A device selected on the configuration page.
/// </summary>
public class PlaybackDeviceSelection
{
    /// <summary>Gets or sets Jellyfin's device id (what the filter matches on).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the device name when it was selected, shown while the device is offline.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets this device's light timing offset in ms on this Hyperion server (-2000 to 2000), or
    /// <see langword="null"/> to use <see cref="PluginConfiguration.LatencyOffsetMilliseconds"/>.
    /// </summary>
    public int? LatencyOffsetMilliseconds { get; set; }
}
