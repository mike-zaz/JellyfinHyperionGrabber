using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.HyperionGrabber.Core.Discovery;

namespace Jellyfin.Plugin.HyperionGrabber.Api;

/// <summary>
/// The Hyperion.ng and HyperHDR servers found on the network, for the configuration page.
/// </summary>
public sealed record DiscoveryResponse
{
    /// <summary>Gets the servers, by name.</summary>
    public IReadOnlyList<DiscoveredServerEntry> Servers { get; init; } = [];

    /// <summary>Creates the response.</summary>
    /// <param name="servers">What the search found.</param>
    /// <returns>The response.</returns>
    internal static DiscoveryResponse From(IEnumerable<DiscoveredServer> servers) => new()
    {
        Servers = servers.Select(s => new DiscoveredServerEntry(s.Kind, s.Name, s.Host, s.FlatBuffersPort)).ToList(),
    };
}
