using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Jellyfin.Plugin.HyperionGrabber.Core.Discovery;

/// <summary>
/// An answer to an SSDP search (<c>HTTP/1.1 200 OK</c> plus headers) from a Hyperion.ng or HyperHDR server.
/// </summary>
internal sealed class SsdpResponse
{
    private readonly Dictionary<string, string> _headers;

    private SsdpResponse(Dictionary<string, string> headers) => _headers = headers;

    /// <summary>Gets <c>Hyperion.ng</c> or <c>HyperHDR</c> from the search target it answered, or <see langword="null"/>
    /// for another kind of device.</summary>
    public string? Kind => Header("ST") switch
    {
        HyperionDiscovery.HyperionNgTarget => "Hyperion.ng",
        HyperionDiscovery.HyperHdrTarget => "HyperHDR",
        _ => null,
    };

    /// <summary>Gets the unique service name, which repeats when a server answers twice.</summary>
    public string? Usn => Header("USN");

    /// <summary>Gets the URL of the server's description document.</summary>
    public string? Location => Header("LOCATION");

    /// <summary>Gets the server's name from its headers (HyperHDR, older Hyperion.ng), if any.</summary>
    public string? Name => Header("HYPERHDR-NAME") ?? Header("HYPERION-NAME");

    /// <summary>Gets the FlatBuffers port from its headers (HyperHDR, older Hyperion.ng), if any.</summary>
    public int? FlatBuffersPort
        => int.TryParse(Header("HYPERHDR-FBS-PORT") ?? Header("HYPERION-FBS-PORT"), NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            && port is >= 1 and <= 65535 ? port : null;

    /// <summary>Parses a datagram; anything but a successful search answer gives <see langword="null"/>.</summary>
    /// <param name="datagram">The received bytes.</param>
    /// <returns>The response, or <see langword="null"/>.</returns>
    public static SsdpResponse? TryParse(ReadOnlySpan<byte> datagram)
    {
        var text = Encoding.UTF8.GetString(datagram);
        var lines = text.Split('\n');
        if (lines.Length == 0 || !lines[0].TrimEnd('\r').StartsWith("HTTP/1.1 200", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.AsSpan(1))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator > 0)
            {
                headers.TryAdd(line[..separator].Trim(), line[(separator + 1)..].Trim());
            }
        }

        return new SsdpResponse(headers);
    }

    private string? Header(string name) => _headers.TryGetValue(name, out var value) && value.Length > 0 ? value : null;
}
