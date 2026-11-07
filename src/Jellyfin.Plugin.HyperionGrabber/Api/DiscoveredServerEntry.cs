namespace Jellyfin.Plugin.HyperionGrabber.Api;

/// <summary>
/// A server in <see cref="DiscoveryResponse"/>.
/// </summary>
/// <param name="Kind"><c>Hyperion.ng</c> or <c>HyperHDR</c>.</param>
/// <param name="Name">The name the server gives itself, or its address.</param>
/// <param name="Host">Its address, to use as the host.</param>
/// <param name="Port">Its FlatBuffers port.</param>
public sealed record DiscoveredServerEntry(string Kind, string Name, string Host, int Port);
