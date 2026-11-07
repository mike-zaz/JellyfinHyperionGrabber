namespace Jellyfin.Plugin.HyperionGrabber.Api;

/// <summary>
/// A library, in <see cref="LibrariesResponse"/>.
/// </summary>
/// <param name="Id">The id of its collection folder.</param>
/// <param name="Name">Its name.</param>
public sealed record LibraryEntry(string Id, string Name);
