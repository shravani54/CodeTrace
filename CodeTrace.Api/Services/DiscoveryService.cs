using System.Net.Sockets;
using System.Text;

namespace CodeTrace.Api.Services;

public class DiscoveryService : BackgroundService
{
    private const int DiscoveryPort = 5081;
    private const int ApiPort = 5080;   // must match the port in --urls

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var udp = new UdpClient(DiscoveryPort);
        var reply = Encoding.UTF8.GetBytes($"CODETRACE_API:{ApiPort}");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(ct);
                if (Encoding.UTF8.GetString(result.Buffer) == "CODETRACE_DISCOVER")
                    await udp.SendAsync(reply, reply.Length, result.RemoteEndPoint);
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore one bad packet */ }
        }
    }
}