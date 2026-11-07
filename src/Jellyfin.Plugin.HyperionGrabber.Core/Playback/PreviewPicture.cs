using System;

namespace Jellyfin.Plugin.HyperionGrabber.Core.Playback;

/// <summary>
/// A copy of the picture last sent to Hyperion, for the live preview on the configuration page.
/// </summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Rgb24">The pixels, 3 bytes (red, green, blue) each, row by row from the top-left corner.</param>
public sealed record PreviewPicture(int Width, int Height, ReadOnlyMemory<byte> Rgb24);
