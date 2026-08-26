using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EMS.Agent.Branding;

namespace EMS.Agent.Services;

/// <summary>
/// Serves the branded "site blocked" page for domains the hosts file points at
/// 127.0.0.1. Listens on loopback :80 (plain HTTP) and :443 (HTTPS, presenting
/// a leaf certificate minted per SNI host by <see cref="BlockCertificateService"/>,
/// which chains to the machine-trusted local root - so the branded page renders
/// over HTTPS with no certificate warning). Raw sockets, no admin URL ACL needed.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WebsiteBlockServer
{
    private readonly ILogger _logger;
    private readonly BlockCertificateService _certs;
    private readonly string _html;
    private TcpListener? _http;
    private TcpListener? _https;
    private CancellationTokenSource? _cts;

    public WebsiteBlockServer(ILogger logger, BlockCertificateService certs)
    {
        _logger = logger;
        _certs = certs;
        _html = BuildPage();
    }

    public void Start()
    {
        if (_cts is not null)
        {
            return;
        }
        _cts = new CancellationTokenSource();
        _http = TryListen(80);
        _https = TryListen(443);

        if (_http is not null)
        {
            _ = Task.Run(() => AcceptLoopAsync(_http, isHttps: false, _cts.Token));
        }
        if (_https is not null)
        {
            _ = Task.Run(() => AcceptLoopAsync(_https, isHttps: true, _cts.Token));
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _http?.Stop();
            _https?.Stop();
        }
        catch { /* best-effort */ }
        finally
        {
            _http = null;
            _https = null;
            _cts = null;
        }
    }

    private TcpListener? TryListen(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            _logger.LogInformation("Website block page listening on 127.0.0.1:{Port}.", port);
            return listener;
        }
        catch (Exception ex)
        {
            // Port already in use (e.g. a local web/dev server). The site is still
            // blocked via the hosts file; only the branded page is unavailable.
            _logger.LogWarning(ex, "Could not bind 127.0.0.1:{Port} for the block page; it may be in use.", port);
            return null;
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener, bool isHttps, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleAsync(client, isHttps), token);
            }
            catch (OperationCanceledException) { break; }
            catch { break; }
        }
    }

    private async Task HandleAsync(TcpClient client, bool isHttps)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                Stream stream = client.GetStream();

                if (isHttps)
                {
                    var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                    var options = new SslServerAuthenticationOptions
                    {
                        ServerCertificateSelectionCallback = (_, hostName) =>
                            _certs.GetLeafForHost(hostName ?? "blocked.local")
                            ?? throw new InvalidOperationException("No block certificate available.")
                    };
                    await ssl.AuthenticateAsServerAsync(options, CancellationToken.None);
                    stream = ssl;
                }

                // Read (and ignore) the request line; we answer everything the same.
                var buffer = new byte[4096];
                await stream.ReadAsync(buffer);
                await WriteResponseAsync(stream, _html);
                await stream.FlushAsync();
            }
        }
        catch
        {
            // Per-connection best-effort (TLS abort, timeout, reset, etc.).
        }
    }

    private static async Task WriteResponseAsync(Stream stream, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var header =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {bytes.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
        await stream.WriteAsync(bytes);
    }

    private static string BuildPage()
    {
        var logo = BrandingAssets.LogoWhiteDataUri();
        var img = string.IsNullOrEmpty(logo)
            ? ""
            : $"<img src=\"{logo}\" alt=\"The Lance\" style=\"width:110px;height:110px;margin-bottom:8px\"/>";
        return
$@"<!doctype html><html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>Blocked by The Lance Endpoint</title></head>
<body style=""margin:0;font-family:Segoe UI,Arial,sans-serif;background:#f3f4f6;color:#06254b"">
<div style=""max-width:520px;margin:10vh auto;background:#fff;border:1px solid #e5e7eb;border-radius:20px;padding:44px;text-align:center;box-shadow:0 10px 40px rgba(6,37,75,.08)"">
  {img}
  <h1 style=""font-size:24px;margin:10px 0 10px"">Site blocked</h1>
  <p style=""font-size:16px;color:#334155;margin:0 0 8px"">This site is blocked by <strong>The Lance Endpoint</strong>.</p>
  <p style=""font-size:15px;color:#64748b;margin:0"">Please contact your administrator.</p>
</div></body></html>";
    }
}
