using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.HyperionGrabber.Frames;

/// <summary>
/// The videos the user chose to keep from driving the lights.
/// </summary>
/// <param name="LibraryIds">Libraries (collection folders) whose videos are skipped.</param>
/// <param name="Tag">A tag that skips an item, or an episode whose season or series has it; empty skips nothing.</param>
/// <param name="FollowExtras">Whether trailers and other extras drive the lights.</param>
public sealed record PlaybackExclusions(IReadOnlySet<Guid> LibraryIds, string Tag, bool FollowExtras)
{
    /// <summary>Gets exclusions that skip nothing.</summary>
    public static PlaybackExclusions None { get; } = new(new HashSet<Guid>(), string.Empty, FollowExtras: true);
}
