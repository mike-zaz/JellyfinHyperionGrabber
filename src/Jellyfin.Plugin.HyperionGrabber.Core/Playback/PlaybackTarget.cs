using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.HyperionGrabber.Core.Hyperion;

namespace Jellyfin.Plugin.HyperionGrabber.Core.Playback;

/// <summary>
/// A Hyperion server that drives the lights of one TV, and the devices whose playback it shows.
/// </summary>
/// <remarks>
/// Hyperion.ng and HyperHDR send a FlatBuffers picture to every running instance, so a target is a server (host, port)
/// and priority, never an instance: two TVs need two servers.
/// </remarks>
public sealed class PlaybackTarget
{
    private PlaybackTarget(string name, HyperionClientOptions? hyperion, FrozenSet<string> deviceIds, FrozenDictionary<string, TimeSpan> latencyOffsets)
    {
        Name = name;
        Hyperion = hyperion;
        DeviceIds = deviceIds;
        LatencyOffsets = latencyOffsets;
        Key = PlaybackTargetKey.Of(hyperion);
    }

    /// <summary>Gets the name shown in the status and the log: the configured name, otherwise the server's address.</summary>
    public string Name { get; }

    /// <summary>Gets the Hyperion server, or <see langword="null"/> when none is configured.</summary>
    public HyperionClientOptions? Hyperion { get; }

    /// <summary>Gets the ids of the devices whose playback drives this target.</summary>
    public IReadOnlySet<string> DeviceIds { get; }

    /// <summary>Gets what identifies the target: the server and priority it registers with.</summary>
    public PlaybackTargetKey Key { get; }

    /// <summary>
    /// Gets the light timing offsets set for single devices, by device id; the other devices use the general offset
    /// (<see cref="Streaming.StreamingSettings.LatencyOffset"/>).
    /// </summary>
    public IReadOnlyDictionary<string, TimeSpan> LatencyOffsets { get; }

    /// <summary>Creates a target.</summary>
    /// <param name="name">Configured name; blank uses the server's address.</param>
    /// <param name="hyperion">The Hyperion server, or <see langword="null"/> when none is configured.</param>
    /// <param name="deviceIds">Selected devices; blank ids are ignored.</param>
    /// <param name="latencyOffsets">Light timing offsets of single devices; offsets of devices that are not selected
    /// are ignored, and the last one wins for a device listed twice.</param>
    /// <returns>The target.</returns>
    public static PlaybackTarget Create(
        string? name,
        HyperionClientOptions? hyperion,
        IEnumerable<string?> deviceIds,
        IEnumerable<KeyValuePair<string, TimeSpan>>? latencyOffsets = null)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);
        var displayName = !string.IsNullOrWhiteSpace(name) ? name.Trim()
            : hyperion is not null ? string.Create(CultureInfo.InvariantCulture, $"{hyperion.Host}:{hyperion.Port}")
            : "Hyperion";
        var ids = deviceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).ToFrozenSet(StringComparer.Ordinal);
        var offsets = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        foreach (var (id, offset) in latencyOffsets ?? [])
        {
            if (!string.IsNullOrEmpty(id) && ids.Contains(id))
            {
                offsets[id] = offset;
            }
        }

        return new PlaybackTarget(displayName, hyperion, ids, offsets.ToFrozenDictionary(StringComparer.Ordinal));
    }
}
