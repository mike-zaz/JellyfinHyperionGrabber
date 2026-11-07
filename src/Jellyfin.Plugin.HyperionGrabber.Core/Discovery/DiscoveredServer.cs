namespace Jellyfin.Plugin.HyperionGrabber.Core.Discovery;

/// <summary>
/// A Hyperion.ng or HyperHDR server that answered the network search.
/// </summary>
/// <param name="Kind"><c>Hyperion.ng</c> or <c>HyperHDR</c>.</param>
/// <param name="Name">The name the server gives itself; its address when it has none.</param>
/// <param name="Host">The address it answered from.</param>
/// <param name="FlatBuffersPort">Its FlatBuffers server port; 19400 when it did not say.</param>
public sealed record DiscoveredServer(string Kind, string Name, string Host, int FlatBuffersPort);
