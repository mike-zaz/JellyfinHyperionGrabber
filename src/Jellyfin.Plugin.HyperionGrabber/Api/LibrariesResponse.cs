using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.HyperionGrabber.Api;

/// <summary>
/// The server's libraries, for the configuration page's list of libraries without lights.
/// </summary>
public sealed record LibrariesResponse
{
    /// <summary>Gets the libraries, sorted by name.</summary>
    public IReadOnlyList<LibraryEntry> Libraries { get; init; } = [];

    /// <summary>Creates the response.</summary>
    /// <param name="folders">Jellyfin's libraries (virtual folders).</param>
    /// <returns>The response.</returns>
    internal static LibrariesResponse From(IEnumerable<VirtualFolderInfo>? folders) => new()
    {
        Libraries = (folders ?? [])
            .Where(f => f is not null && !string.IsNullOrEmpty(f.ItemId))
            .Select(f => new LibraryEntry(f.ItemId, string.IsNullOrWhiteSpace(f.Name) ? f.ItemId : f.Name))
            .OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList(),
    };
}
