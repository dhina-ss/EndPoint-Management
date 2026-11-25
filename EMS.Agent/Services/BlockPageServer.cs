using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using EMS.Agent.Branding;

namespace EMS.Agent.Services;

/// <summary>
/// A tiny loopback web server that serves the branded "internet blocked - please
/// sign in" page while the require-login policy holds a session. Browsers reach
/// it via the system proxy (and it is reachable on 127.0.0.1 even while the
/// firewall blocks the internet). A "Sign in" click launches the activation
/// window. Uses a raw TcpListener so it needs no admin URL reservation.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class BlockPageServer
{
    public const int Port = 47624;

    private readonly ILogger _logger;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly string _html;

    public BlockPageServer(ILogger logger)
    {
        _logger = logger;
        _html = BuildPage();
    }

    public void Start()
    {
        if (_listener is not null)
        {
            return;
        }

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
            _logger.LogInformation("Block page server started on 127.0.0.1:{Port}.", Port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not start the block page server.");
            _listener = null;
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
        }
        catch { /* best-effort */ }
        finally
        {
            _listener = null;
            _cts = null;
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleAsync(client), token);
            }
            catch (OperationCanceledException) { break; }
            catch { /* listener stopped */ break; }
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 3000;
                using var stream = client.GetStream();
                var buffer = new byte[4096];
                var read = await stream.ReadAsync(buffer);
                var request = Encoding.ASCII.GetString(buffer, 0, read);
                var firstLine = request.Split('\n').FirstOrDefault() ?? string.Empty;

                // HTTPS tunnels can't be served without a certificate; close them
                // (the browser shows an error, and the HTTP paths show the page).
                if (firstLine.StartsWith("CONNECT", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (firstLine.Contains("/__signin", StringComparison.OrdinalIgnoreCase))
                {
                    LaunchActivation();
                    await WriteResponseAsync(stream,
                        "<html><body style='font-family:Segoe UI;text-align:center;margin-top:80px'>" +
                        "<h2>Opening sign-in…</h2><p>Complete sign-in in the window, then browse normally.</p></body></html>");
                    return;
                }

                await WriteResponseAsync(stream, _html);
            }
        }
        catch { /* per-connection best-effort */ }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, string body)
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

    private static void LaunchActivation()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                Process.Start(new ProcessStartInfo(exe, "--login") { UseShellExecute = false });
            }
        }
        catch { /* best-effort */ }
    }

    private static string BuildPage()
    {
        var logo = BrandingAssets.LogoWhiteDataUri();
        var img = string.IsNullOrEmpty(logo) ? "" : $"<img src=\"{logo}\" alt=\"The Lance\" style=\"width:110px;height:110px;margin-bottom:8px\"/>";
        return
$@"<!doctype html><html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>The Lance Endpoint</title></head>
<body style=""margin:0;font-family:Segoe UI,Arial,sans-serif;background:#f3f4f6;color:#06254b"">
<div style=""max-width:520px;margin:8vh auto;background:#fff;border:1px solid #e5e7eb;border-radius:20px;padding:40px;text-align:center;box-shadow:0 10px 40px rgba(6,37,75,.08)"">
  {img}
  <h1 style=""font-size:24px;margin:8px 0 6px"">Internet blocked</h1>
  <p style=""font-size:16px;color:#334155;margin:0 0 24px"">The Lance Endpoint blocked the internet, please login to continue browse.</p>
  <a href=""http://thelance.local/__signin"" style=""display:inline-block;background:#06254b;color:#fff;text-decoration:none;font-weight:600;padding:12px 28px;border-radius:12px"">Sign in to continue</a>
  <p style=""font-size:12px;color:#94a3b8;margin-top:22px"">Enter your employee code or email address and password to restore access.</p>
</div></body></html>";
    }
}
