using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.HyperionGrabber.Core.Tests.Discovery;

/// <summary>
/// Answers SSDP searches on loopback like Hyperion.ng or HyperHDR: every search whose <c>ST</c> matches a reply's
/// target gets that reply, sent to the searching socket.
/// </summary>
internal sealed class FakeSsdpResponder : IAsyncDisposable
{
    private readonly UdpClient _udp = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentQueue<string> _requests = new();
    private readonly (string Target, string Reply)[] _replies;
    private readonly Task _answering;

    public FakeSsdpResponder(params (string Target, string Reply)[] replies)
    {
        _replies = replies;
        _answering = AnswerAsync();
    }

    public IPEndPoint EndPoint => (IPEndPoint)_udp.Client.LocalEndPoint!;

    /// <summary>Gets the searches received, oldest first.</summary>
    public IReadOnlyList<string> Requests => _requests.ToArray();

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        await _answering;
        _udp.Dispose();
        _stopping.Dispose();
    }

    private async Task AnswerAsync()
    {
        try
        {
            while (true)
            {
                var received = await _udp.ReceiveAsync(_stopping.Token);
                var request = Encoding.ASCII.GetString(received.Buffer);
                _requests.Enqueue(request);
                foreach (var (target, reply) in _replies)
                {
                    if (request.Contains($"ST: {target}\r\n", StringComparison.Ordinal))
                    {
                        await _udp.SendAsync(Encoding.ASCII.GetBytes(reply), received.RemoteEndPoint, _stopping.Token);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }
}
