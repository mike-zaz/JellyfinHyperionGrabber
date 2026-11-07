namespace Jellyfin.Plugin.HyperionGrabber.Core.Playback;

/// <summary>
/// Tells what the lights are doing, for the status panel.
/// </summary>
public interface IPlaybackStatusSource
{
    /// <summary>Describes the playback that drives the lights and its session. Thread-safe; returns at once.</summary>
    /// <returns>A snapshot.</returns>
    PlaybackMonitorStatus GetStatus();

    /// <summary>Copies the picture last sent to a Hyperion server, for the live preview. Thread-safe; returns at once.</summary>
    /// <param name="targetIndex">The server's position in <see cref="PlaybackMonitorStatus.Targets"/>.</param>
    /// <returns>The picture, or <see langword="null"/> when nothing plays on that server (or there is no such server).</returns>
    PreviewPicture? GetPreview(int targetIndex) => null;
}
