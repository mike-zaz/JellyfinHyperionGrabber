using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Jellyfin.Plugin.HyperionGrabber.Core.Hyperion;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HyperionGrabber.Core.Discovery;

/// <summary>
/// Finds Hyperion.ng and HyperHDR servers on the local network with SSDP, which both answer.
/// </summary>
/// <remarks>
/// <para>One search per kind goes to the SSDP multicast group (239.255.255.250:1900); answers come back for
/// <see cref="ListenTime"/>. HyperHDR names its FlatBuffers port in the answer (<c>HYPERHDR-FBS-PORT</c>). Hyperion.ng
/// puts it in its description document (<c>LOCATION</c>, <c>&lt;ports&gt;&lt;flatBuffer&gt;</c>), which is fetched only
/// from the address that answered, without redirects, within <see cref="DescriptionTimeout"/> and up to
/// <see cref="MaxDescriptionLength"/> bytes: a device on the network can make the server read from itself only.</para>
/// <para>Multicast reaches the network only when the Jellyfin server is on it (Docker: host networking).</para>
/// </remarks>
public sealed partial class HyperionDiscovery : IDisposable
{
    /// <summary>The search target Hyperion.ng answers.</summary>
    internal const string HyperionNgTarget = "urn:hyperion-project.org:device:basic:1";

    /// <summary>The search target HyperHDR answers.</summary>
    internal const string HyperHdrTarget = "urn:hyperhdr.eu:device:basic:1";

    /// <summary>At most this many servers are listed.</summary>
    internal const int MaxServers = 16;

    /// <summary>Larger description documents are not read.</summary>
    internal const int MaxDescriptionLength = 64 * 1024;

    /// <summary>How long answers are collected.</summary>
    internal static readonly TimeSpan ListenTime = TimeSpan.FromSeconds(2);

    /// <summary>How long fetching one description document may take.</summary>
    internal static readonly TimeSpan DescriptionTimeout = TimeSpan.FromSeconds(2);

    private static readonly IPEndPoint SsdpMulticast = new(IPAddress.Parse("239.255.255.250"), 1900);

    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly IPEndPoint _target;
    private readonly TimeSpan _listenTime;

    /// <summary>Initializes a new instance of the <see cref="HyperionDiscovery"/> class.</summary>
    /// <param name="logger">The logger.</param>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The HttpClient made by the other constructor owns the handler and disposes it in Dispose.")]
    public HyperionDiscovery(ILogger<HyperionDiscovery> logger)
        : this(new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = DescriptionTimeout }, logger, SsdpMulticast, ListenTime)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HyperionDiscovery"/> class.</summary>
    /// <param name="handler">Fetches description documents; disposed with the discovery.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="target">Where the searches go: the multicast group, or a test's responder.</param>
    /// <param name="listenTime">How long answers are collected.</param>
    internal HyperionDiscovery(HttpMessageHandler handler, ILogger logger, IPEndPoint target, TimeSpan listenTime)
    {
        _http = new HttpClient(handler) { Timeout = DescriptionTimeout, MaxResponseContentBufferSize = MaxDescriptionLength };
        _logger = logger;
        _target = target;
        _listenTime = listenTime;
    }

    /// <summary>Searches the network.</summary>
    /// <param name="cancellationToken">Stops the search.</param>
    /// <returns>The servers that answered, by name.</returns>
    public async Task<IReadOnlyList<DiscoveredServer>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var answers = await SearchAsync(cancellationToken).ConfigureAwait(false);
        var servers = new List<DiscoveredServer>(answers.Count);
        foreach (var (address, answer) in answers)
        {
            servers.Add(await DescribeAsync(address, answer, cancellationToken).ConfigureAwait(false));
        }

        Log.Found(_logger, servers.Count);
        return servers.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(s => s.Host, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    /// <summary>The search for one kind of server, as SSDP's <c>M-SEARCH</c>.</summary>
    internal static byte[] Request(string target) => Encoding.ASCII.GetBytes(
        "M-SEARCH * HTTP/1.1\r\n"
        + "HOST: 239.255.255.250:1900\r\n"
        + "MAN: \"ssdp:discover\"\r\n"
        + "MX: 1\r\n"
        + $"ST: {target}\r\n"
        + "\r\n");

    /// <summary>Reads the FlatBuffers port and the name from a Hyperion.ng description document.</summary>
    internal static (int? FlatBuffersPort, string? Name) ParseDescription(Stream xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxDescriptionLength };
        using var reader = XmlReader.Create(xml, settings);
        (int? port, string? name) = (null, null);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.LocalName == "flatBuffer"
                && int.TryParse(reader.ReadElementContentAsString(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                && value is >= 1 and <= 65535)
            {
                port = value;
            }
            else if (reader.LocalName == "friendlyName")
            {
                name = reader.ReadElementContentAsString().Trim();
            }
        }

        return (port, string.IsNullOrEmpty(name) ? null : name);
    }

    /// <summary>The description's URL, only if it is HTTP(S) on the address that answered.</summary>
    private static Uri? DescriptionAddress(string? location, IPAddress answeredFrom)
        => Uri.TryCreate(location, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && IPAddress.TryParse(uri.Host, out var host)
            && host.Equals(answeredFrom)
                ? uri
                : null;

    private async Task<List<(IPAddress Address, SsdpResponse Answer)>> SearchAsync(CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
        foreach (var target in new[] { HyperionNgTarget, HyperHdrTarget })
        {
            await socket.SendToAsync(Request(target), SocketFlags.None, _target, cancellationToken).ConfigureAwait(false);
        }

        var answers = new List<(IPAddress, SsdpResponse)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new byte[8192];
        using var listening = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        listening.CancelAfter(_listenTime);
        while (answers.Count < MaxServers)
        {
            SocketReceiveFromResult received;
            try
            {
                received = await socket.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), listening.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break; // Listened long enough.
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                continue; // Windows reports an ICMP "port unreachable" for an earlier datagram here.
            }

            if (received.RemoteEndPoint is IPEndPoint from
                && SsdpResponse.TryParse(buffer.AsSpan(0, received.ReceivedBytes)) is { Kind: not null } answer
                && seen.Add(answer.Usn ?? from.ToString()))
            {
                answers.Add((from.Address, answer));
            }
        }

        return answers;
    }

    private async Task<DiscoveredServer> DescribeAsync(IPAddress address, SsdpResponse answer, CancellationToken cancellationToken)
    {
        var host = address.ToString();
        var (port, name) = (answer.FlatBuffersPort, answer.Name);
        if ((port is null || name is null) && DescriptionAddress(answer.Location, address) is { } location)
        {
            var description = await FetchDescriptionAsync(location, cancellationToken).ConfigureAwait(false);
            port ??= description.FlatBuffersPort;
            name ??= description.Name;
        }

        Log.Answered(_logger, answer.Kind!, host, port ?? HyperionDefaults.FlatBuffersPort);
        return new DiscoveredServer(answer.Kind!, name ?? host, host, port ?? HyperionDefaults.FlatBuffersPort);
    }

    private async Task<(int? FlatBuffersPort, string? Name)> FetchDescriptionAsync(Uri location, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(location, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, null);
            }

            var xml = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (xml.ConfigureAwait(false))
            {
                return ParseDescription(xml);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException or IOException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            Log.DescriptionFailed(_logger, location, ex.Message);
            return (null, null);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Network search found {Count} Hyperion.ng or HyperHDR server(s)")]
        public static partial void Found(ILogger logger, int count);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "{Kind} answered from {Host}, FlatBuffers port {Port}")]
        public static partial void Answered(ILogger logger, string kind, string host, int port);

        [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Could not read the description at {Location}: {Reason}")]
        public static partial void DescriptionFailed(ILogger logger, Uri location, string reason);
    }
}
