namespace Jellyfin.Plugin.HyperionGrabber.Configuration;

/// <summary>
/// A library selected on the configuration page.
/// </summary>
public class PlaybackLibrarySelection
{
    /// <summary>Gets or sets the id of the library's collection folder.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the library name when it was selected, shown if the library is gone.</summary>
    public string Name { get; set; } = string.Empty;
}
