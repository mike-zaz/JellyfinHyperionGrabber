using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HyperionGrabber.Core.Discovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Jellyfin.Plugin.HyperionGrabber.Core.Tests.Discovery;

/// <summary>
/// The network search against a fake SSDP responder on loopback, with the answers Hyperion.ng and HyperHDR send
/// (libsrc/ssdp and sources/ssdp in their repositories).
/// </summary>
public class HyperionDiscoveryTests
{
    // Short, because every search listens this long for answers.
    private static readonly TimeSpan ListenTime = TimeSpan.FromMilliseconds(300);

    private readonly StubHandler _http = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HyperHdr_ItsAnswerGivesThePortAndTheName()
    {
        await using var responder = new FakeSsdpResponder((HyperionDiscovery.HyperHdrTarget, HyperHdrAnswer("Living room", 19401)));

        var server = Assert.Single(await DiscoverAsync(responder));

        Assert.Equal(new DiscoveredServer("HyperHDR", "Living room", "127.0.0.1", 19401), server);
        Assert.Empty(_http.Requested); // Nothing to look up.
    }

    [Fact]
    public async Task HyperionNg_ItsDescriptionGivesThePortAndTheName()
    {
        _http.Body = Description("Hyperion (living room)", flatBuffer: 19402);
        await using var responder = new FakeSsdpResponder((HyperionDiscovery.HyperionNgTarget, HyperionNgAnswer("http://127.0.0.1:8090/description.xml")));

        var server = Assert.Single(await DiscoverAsync(responder));

        Assert.Equal(new DiscoveredServer("Hyperion.ng", "Hyperion (living room)", "127.0.0.1", 19402), server);
        Assert.Equal([new Uri("http://127.0.0.1:8090/description.xml")], _http.Requested);
    }

    [Fact]
    public async Task DescriptionOnAnotherHost_IsNotFetched()
    {
        // A device may only make the server read from itself.
        await using var responder = new FakeSsdpResponder((HyperionDiscovery.HyperionNgTarget, HyperionNgAnswer("http://10.1.2.3:8090/description.xml")));

        var server = Assert.Single(await DiscoverAsync(responder));

        Assert.Empty(_http.Requested);
        Assert.Equal(("127.0.0.1", 19400), (server.Name, server.FlatBuffersPort));
    }

    [Fact]
    public async Task UnreadableDescription_UsesTheDefaultPort()
    {
        _http.Body = "<root><ports><flatBuffer>not a port";
        await using var responder = new FakeSsdpResponder((HyperionDiscovery.HyperionNgTarget, HyperionNgAnswer("http://127.0.0.1:8090/description.xml")));

        var server = Assert.Single(await DiscoverAsync(responder));

        Assert.Equal(19400, server.FlatBuffersPort);
    }

    [Fact]
    public async Task Descriptions_AreFetchedAtTheSameTime()
    {
        // The stub answers only once both requests arrived. One after the other, the first would time out (2 s each,
        // up to 16 servers) and miss its port.
        (_http.Body, _http.WaitForRequests) = (Description("Hyperion", flatBuffer: 19402), 2);
        await using var responder = new FakeSsdpResponder(
            (HyperionDiscovery.HyperionNgTarget, HyperionNgAnswer("http://127.0.0.1:8090/description.xml", usn: "uuid:ng-1")),
            (HyperionDiscovery.HyperionNgTarget, HyperionNgAnswer("http://127.0.0.1:8091/description.xml", usn: "uuid:ng-2")));

        var servers = await DiscoverAsync(responder);

        Assert.Equal(2, servers.Count);
        Assert.All(servers, server => Assert.Equal(19402, server.FlatBuffersPort));
    }

    [Fact]
    public async Task SearchThatCannotBeSent_FindsNothingAndLogsWhy()
    {
        // Sending to the broadcast address without permission fails like a network without a multicast route: with a
        // SocketException, which must not become an error response.
        var logger = new FakeLogger();
        using var discovery = new HyperionDiscovery(_http, logger, new IPEndPoint(IPAddress.Broadcast, 1900), ListenTime);

        Assert.Empty(await discovery.DiscoverAsync(Ct));
        Assert.Contains(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning && r.Message.StartsWith("Could not search the network", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ServerAnsweringTwice_IsListedOnce()
    {
        var answer = HyperHdrAnswer("Living room", 19400);
        await using var responder = new FakeSsdpResponder((HyperionDiscovery.HyperHdrTarget, answer), (HyperionDiscovery.HyperHdrTarget, answer));

        Assert.Single(await DiscoverAsync(responder));
    }

    [Fact]
    public async Task AnswerOfAnotherKindOfDevice_IsIgnored()
    {
        var other = HyperHdrAnswer("Router", 19400).Replace(HyperionDiscovery.HyperHdrTarget, "urn:schemas-upnp-org:device:InternetGatewayDevice:1", StringComparison.Ordinal);
        await using var responder = new FakeSsdpResponder((HyperionDiscovery.HyperHdrTarget, other));

        Assert.Empty(await DiscoverAsync(responder));
    }

    [Fact]
    public async Task SearchesForBothKinds()
    {
        await using var responder = new FakeSsdpResponder();

        await DiscoverAsync(responder);

        Assert.Equal(
            [HyperionDiscovery.HyperionNgTarget, HyperionDiscovery.HyperHdrTarget],
            responder.Requests.Select(r => r.Split("\r\n").Single(l => l.StartsWith("ST: ", StringComparison.Ordinal))[4..]));
        Assert.All(responder.Requests, r => Assert.StartsWith("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 1\r\n", r, StringComparison.Ordinal));
    }

    [Fact]
    public void ParseDescription_ReadsHyperionNgsDocument()
    {
        using var xml = new MemoryStream(Encoding.UTF8.GetBytes(Description("Hyperion", flatBuffer: 19400)));

        Assert.Equal((19400, "Hyperion"), HyperionDiscovery.ParseDescription(xml));
    }

    [Fact]
    public void ParseDescription_RefusesADocumentType()
    {
        using var xml = new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE root [<!ENTITY x \"y\">]><root><friendlyName>&x;</friendlyName></root>"));

        Assert.ThrowsAny<System.Xml.XmlException>(() => HyperionDiscovery.ParseDescription(xml));
    }

    private static string HyperHdrAnswer(string name, int port)
        => "HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age = 1800\r\nDATE: Sat, 07 Nov 2026 12:00:00 GMT\r\nEXT: \r\n"
            + "LOCATION: http://127.0.0.1:8090/description.xml\r\nSERVER: Linux/6.1 UPnP/1.0 HyperHDR/22.0.0\r\n"
            + $"ST: {HyperionDiscovery.HyperHdrTarget}\r\nUSN: uuid:5a0f7d5b-0000-4000-8000-00000000hdr1\r\n"
            + $"HYPERHDR-FBS-PORT: {port}\r\nHYPERHDR-JSS-PORT: 19444\r\nHYPERHDR-NAME: {name}\r\n\r\n";

    private static string HyperionNgAnswer(string location, string usn = "uuid:5a0f7d5b-0000-4000-8000-0000000000ng")
        => "HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age = 1800\r\nDATE: Sat, 07 Nov 2026 12:00:00 GMT\r\nEXT: \r\n"
            + $"LOCATION: {location}\r\nSERVER: Linux/6.1 UPnP/1.0 Hyperion/2.1.1\r\n"
            + $"ST: {HyperionDiscovery.HyperionNgTarget}\r\nUSN: {usn}\r\n\r\n";

    // Hyperion.ng's libsrc/ssdp/SSDPDescription.h, filled in.
    private static string Description(string name, int flatBuffer)
        => "<?xml version=\"1.0\"?><root xmlns=\"urn:schemas-upnp-org:device-1-0\"><specVersion><major>1</major><minor>0</minor></specVersion>"
            + "<URLBase>http://127.0.0.1:8090</URLBase><device><deviceType>urn:schemas-upnp-org:device:Basic:1</deviceType>"
            + $"<friendlyName>{name}</friendlyName><manufacturer>Hyperion Open Source Ambient Lighting</manufacturer><modelName>Hyperion</modelName>"
            + $"<ports><jsonServer>19444</jsonServer><sslServer>8092</sslServer><protoBuffer>19445</protoBuffer><flatBuffer>{flatBuffer}</flatBuffer></ports>"
            + "<presentationURL>index.html</presentationURL></device></root>";

    private async Task<System.Collections.Generic.IReadOnlyList<DiscoveredServer>> DiscoverAsync(FakeSsdpResponder responder)
    {
        using var discovery = new HyperionDiscovery(_http, NullLogger.Instance, responder.EndPoint, ListenTime);
        return await discovery.DiscoverAsync(Ct);
    }

    /// <summary>Answers every request with <see cref="Body"/> and records the requested addresses.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly ConcurrentQueue<Uri> _requested = new();
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Body { get; set; } = string.Empty;

        /// <summary>Gets or sets how many requests must have arrived before any is answered.</summary>
        public int WaitForRequests { get; set; }

        public Uri[] Requested => _requested.ToArray();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requested.Enqueue(request.RequestUri!);
            if (_requested.Count >= WaitForRequests)
            {
                _allArrived.TrySetResult();
            }

            await _allArrived.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body, Encoding.UTF8, "text/xml") };
        }
    }
}
