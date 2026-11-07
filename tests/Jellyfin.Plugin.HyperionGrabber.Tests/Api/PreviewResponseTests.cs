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

        Assert.Equal((true, 2, 1, "/wAAAAD/"), (response.Available, response.Width, response.Height, response.Pixels));
    }

    [Fact]
    public void From_NoPicture_IsNotAvailable()
    {
        var response = PreviewResponse.From(null);

        Assert.Equal((false, 0, 0, string.Empty), (response.Available, response.Width, response.Height, response.Pixels));
    }
}
