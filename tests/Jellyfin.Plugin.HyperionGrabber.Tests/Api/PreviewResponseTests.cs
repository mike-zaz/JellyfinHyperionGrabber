using System.Text.Json;
using Jellyfin.Plugin.HyperionGrabber.Api;
using Jellyfin.Plugin.HyperionGrabber.Core.Playback;
using Xunit;

namespace Jellyfin.Plugin.HyperionGrabber.Tests.Api;

public class PreviewResponseTests
{
    [Fact]
    public void From_APicture_GivesItsSizeAndPixelsAsBase64()
    {
        var response = PreviewResponse.From(new PreviewPicture(2, 1, new byte[] { 255, 0, 0, 0, 0, 255 }));

        Assert.Equal(
            """{"Available":true,"Width":2,"Height":1,"Pixels":"/wAAAAD/"}""",
            JsonSerializer.Serialize(response));
    }

    [Fact]
    public void From_NoPicture_IsNotAvailable()
    {
        var response = PreviewResponse.From(null);

        Assert.Equal(
            """{"Available":false,"Width":0,"Height":0,"Pixels":""}""",
            JsonSerializer.Serialize(response));
    }
}
