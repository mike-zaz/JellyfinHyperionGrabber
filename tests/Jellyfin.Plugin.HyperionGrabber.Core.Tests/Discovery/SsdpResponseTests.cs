using System.Text;
using Jellyfin.Plugin.HyperionGrabber.Core.Discovery;
using Xunit;

namespace Jellyfin.Plugin.HyperionGrabber.Core.Tests.Discovery;

public class SsdpResponseTests
{
    [Fact]
    public void TryParse_ReadsHeadersIgnoringCase()
    {
        var response = SsdpResponse.TryParse(Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nst: {HyperionDiscovery.HyperHdrTarget}\r\nusn: uuid:1\r\nLocation: http://192.168.1.20:8090/description.xml\r\nHyperHDR-FBS-Port: 19401\r\nhyperhdr-name: TV\r\n\r\n"));

        Assert.NotNull(response);
        Assert.Equal(("HyperHDR", "uuid:1", "http://192.168.1.20:8090/description.xml", 19401, "TV"), (response.Kind, response.Usn, response.Location, response.FlatBuffersPort, response.Name));
    }

    [Theory]
    [InlineData("NOTIFY * HTTP/1.1\r\nNT: urn:hyperhdr.eu:device:basic:1\r\n\r\n")] // An announcement, not an answer.
    [InlineData("M-SEARCH * HTTP/1.1\r\nST: urn:hyperhdr.eu:device:basic:1\r\n\r\n")] // Another client's search.
    [InlineData("")]
    public void TryParse_AnythingButAnAnswer_IsNull(string datagram)
    {
        Assert.Null(SsdpResponse.TryParse(Encoding.ASCII.GetBytes(datagram)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("-1")]
    [InlineData("port")]
    public void TryParse_ImpossiblePort_IsNoPort(string port)
    {
        var response = SsdpResponse.TryParse(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nST: {HyperionDiscovery.HyperHdrTarget}\r\nHYPERHDR-FBS-PORT: {port}\r\n\r\n"));

        Assert.Null(response!.FlatBuffersPort);
    }
}
