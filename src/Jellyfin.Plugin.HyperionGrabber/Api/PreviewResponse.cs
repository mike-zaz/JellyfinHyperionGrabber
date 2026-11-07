using System;
using Jellyfin.Plugin.HyperionGrabber.Core.Playback;

namespace Jellyfin.Plugin.HyperionGrabber.Api;

/// <summary>
/// The picture last sent to a Hyperion server, for the live preview on the configuration page.
/// </summary>
public sealed record PreviewResponse
{
    /// <summary>Gets a value indicating whether there is a picture: false while nothing plays on that server.</summary>
    public bool Available { get; init; }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Gets the pixels, 3 bytes (red, green, blue) each, row by row from the top-left corner; JSON carries them
    /// as base64.</summary>
    /// <remarks>System.Text.Json writes the base64 straight into the response. A base64 string would be 115-154 KB, on
    /// the large object heap, for every request while the preview is shown.</remarks>
    public ReadOnlyMemory<byte> Pixels { get; init; }

    /// <summary>Creates the response.</summary>
    /// <param name="picture">The picture, or <see langword="null"/> when there is none.</param>
    /// <returns>The response.</returns>
    internal static PreviewResponse From(PreviewPicture? picture) => picture is null
        ? new PreviewResponse()
        : new PreviewResponse
        {
            Available = true,
            Width = picture.Width,
            Height = picture.Height,
            Pixels = picture.Rgb24,
        };
}
