using Jellyfin.Plugin.HyperionGrabber.Api;
using Jellyfin.Plugin.HyperionGrabber.Core.Discovery;
using Xunit;

namespace Jellyfin.Plugin.HyperionGrabber.Tests.Api;

public class DiscoveryResponseTests
{
    [Fact]
    public void From_ListsEachServerWithItsPort()
    {
        var response = DiscoveryResponse.From([new DiscoveredServer("HyperHDR", "Living room", "192.168.1.20", 19401)]);

        Assert.Equal(new DiscoveredServerEntry("HyperHDR", "Living room", "192.168.1.20", 19401), Assert.Single(response.Servers));
    }
}
