using System.Linq;
using Jellyfin.Plugin.HyperionGrabber.Api;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.HyperionGrabber.Tests.Api;

public class LibrariesResponseTests
{
    [Fact]
    public void From_ListsTheLibrariesByName_AndSkipsOnesWithoutAnId()
    {
        var response = LibrariesResponse.From(
        [
            new VirtualFolderInfo { ItemId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", Name = "Movies" },
            new VirtualFolderInfo { ItemId = string.Empty, Name = "Broken" },
            new VirtualFolderInfo { ItemId = "cccccccccccccccccccccccccccccccc", Name = "Kids" },
        ]);

        Assert.Equal(["Kids", "Movies"], response.Libraries.Select(l => l.Name));
        Assert.Equal("cccccccccccccccccccccccccccccccc", response.Libraries[0].Id);
    }

    [Fact]
    public void From_Nothing_IsEmpty()
    {
        Assert.Empty(LibrariesResponse.From(null).Libraries);
    }
}
