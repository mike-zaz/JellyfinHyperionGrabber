using System;
using System.Collections.Generic;
using Jellyfin.Plugin.HyperionGrabber.Core.Hyperion;
using Jellyfin.Plugin.HyperionGrabber.Core.Playback;
using Xunit;

namespace Jellyfin.Plugin.HyperionGrabber.Core.Tests.Playback;

public class PlaybackTargetTests
{
    private static readonly HyperionClientOptions Hyperion = new() { Host = "hyperion.local" };

    [Fact]
    public void Create_WithoutOffsets_HasNone()
    {
        var target = PlaybackTarget.Create("TV", Hyperion, ["kodi"]);

        Assert.Empty(target.LatencyOffsets);
    }

    [Fact]
    public void Create_KeepsTheOffsetsOfSelectedDevicesOnly()
    {
        var target = PlaybackTarget.Create(
            "TV",
            Hyperion,
            ["kodi", "shield"],
            [KeyValuePair.Create("kodi", TimeSpan.FromMilliseconds(120)), KeyValuePair.Create("phone", TimeSpan.FromMilliseconds(50))]);

        var offset = Assert.Single(target.LatencyOffsets);
        Assert.Equal(("kodi", TimeSpan.FromMilliseconds(120)), (offset.Key, offset.Value));
    }

    [Fact]
    public void Create_DeviceListedTwice_TheLastOffsetWins()
    {
        var target = PlaybackTarget.Create(
            "TV",
            Hyperion,
            ["kodi"],
            [KeyValuePair.Create("kodi", TimeSpan.FromMilliseconds(120)), KeyValuePair.Create("kodi", TimeSpan.FromMilliseconds(-80))]);

        Assert.Equal(TimeSpan.FromMilliseconds(-80), target.LatencyOffsets["kodi"]);
    }
}
