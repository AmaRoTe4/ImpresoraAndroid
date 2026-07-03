using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using PrintAgentAndroid.Printing;

namespace PrintAgentAndroid.Http;

public sealed class LocalHttpServer
{
    private readonly UsbEscPosPrinter _printer;
    private readonly Action<string> _log;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _port;
    private IPAddress _bindAddress;

    public int Port => _port;
    public string BindAddress => _bindAddress.ToString();

    public LocalHttpServer(UsbEscPosPrinter printer, Action<string> log, int port = 5000)
    {
        _printer = printer;
        _log = log;
        _port = port;
        _bindAddress = IPAddress.Loopback;
    }

    public Task StartAsync()
    {
        if (_listener != null) return Task.CompletedTask;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(_bindAddress, _port);
        _listener.Start();
        _log($"HTTP local iniciado en http://{_bindAddress}:{_port}");
        _ = Task.Run(() => AcceptLoop(_cts.Token));
        return Task.CompletedTask;
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        _listener = null;
    }

    public void Reconfigure(int? newPort = null, string? host = null, bool? listenAll = null)
    {
        var changed = false;

        if (newPort.HasValue && newPort.Value != _port)
        {
            _port = newPort.Value;
            changed = true;
        }

        if (listenAll == true)
        {
            if (!Equals(_bindAddress, IPAddress.Any))
            {
                _bindAddress = IPAddress.Any;
                changed = true;
            }
        }
        else if (!string.IsNullOrEmpty(host) && IPAddress.TryParse(host, out var parsed))
        {
            if (!Equals(_bindAddress, parsed))
            {
                _bindAddress = parsed;
                changed = true;
            }
        }
        else if (listenAll == false && !Equals(_bindAddress, IPAddress.Loopback))
        {
            _bindAddress = IPAddress.Loopback;
            changed = true;
        }

        if (!changed) return;

        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(_bindAddress, _port);
        _listener.Start();
        _log($"HTTP reiniciado en http://{_bindAddress}:{_port}");
        _ = Task.Run(() => AcceptLoop(_cts.Token));
    }

    private async Task AcceptLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClient(client), token);
            }
            catch when (token.IsCancellationRequested) { }
            catch (Exception ex) { _log("HTTP Accept error: " + ex.Message); }
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        await using var _ = client;
        try
        {
            using var stream = client.GetStream();
            var request = await ReadRequest(stream);
            if (request == null)
            {
                await WriteJson(stream, 400, new { error = "bad-request" });
                return;
            }

            if (request.Method == "OPTIONS")
            {
                await WriteRaw(stream, 204, "", "text/plain");
                return;
            }

            await Route(stream, request);
        }
        catch (Exception ex)
        {
            _log("HTTP error: " + ex.Message);
        }
    }

    private async Task Route(NetworkStream stream, HttpRequest request)
    {
        try
        {
            if (request.Method == "GET" && request.Path == "/status")
            {
                var device = _printer.FindPrinterDevice();
                await WriteJson(stream, 200, new
                {
                    status = "ok",
                    server = "running",
                    port = _port,
                    printerConnected = device != null,
                    printer = device == null ? null : new
                    {
                        name = device.DeviceName,
                        vendorId = device.VendorId,
                        productId = device.ProductId,
                        hasPermission = _printer.HasPermission(device)
                    }
                });
                return;
            }

            if (request.Method == "GET" && request.Path == "/printers")
            {
                await WriteJson(stream, 200, new
                {
                    defaultPrinter = "USB",
                    preferredPrinter = "USB",
                    printers = _printer.ListDevices()
                });
                return;
            }

            if (request.Method == "POST" && request.Path == "/config")
            {
                if (string.IsNullOrWhiteSpace(request.Body))
                {
                    await WriteJson(stream, 400, new { error = "Body vacío. Enviar JSON con host/port/listen_all" });
                    return;
                }

                using var cfgJson = JsonDocument.Parse(request.Body);
                var cfg = cfgJson.RootElement;

                int? newPort = cfg.TryGetProperty("port", out var portEl) && portEl.TryGetInt32(out var p) ? p : null;
                var host = cfg.TryGetProperty("host", out var hostEl) ? hostEl.GetString() : null;
                bool? listenAll = cfg.TryGetProperty("listen_all", out var listenEl) ? listenEl.GetBoolean() : null;

                Reconfigure(newPort, host, listenAll);

                await WriteJson(stream, 200, new
                {
                    status = "ok",
                    port = _port,
                    host = _bindAddress.ToString(),
                    listenAll = Equals(_bindAddress, IPAddress.Any)
                });
                return;
            }

            if (request.Method == "POST" && request.Path == "/test")
            {
                await _printer.PrintAsync(EscPosTicketBuilder.BuildRawText("TEST PRINTAGENT ANDROID\nhttp://127.0.0.1:5000\n"));
                await WriteJson(stream, 200, new { status = "printed" });
                return;
            }

            if (request.Method == "POST" && (request.Path == "/print" || request.Path == "/print_text"))
            {
                using var json = JsonDocument.Parse(request.Body);
                if (!json.RootElement.TryGetProperty("text", out var textEl))
                {
                    await WriteJson(stream, 400, new { error = "Falta campo 'text'" });
                    return;
                }
                await _printer.PrintAsync(EscPosTicketBuilder.BuildRawText(textEl.GetString() ?? ""));
                await WriteJson(stream, 200, new { status = "printed_with_cut" });
                return;
            }

            if (request.Method == "POST" && request.Path == "/print_ticket")
            {
                await _printer.PrintAsync(EscPosTicketBuilder.BuildTicket(request.Body));
                await WriteJson(stream, 200, new { status = "printed" });
                return;
            }

            if (request.Method == "POST" && (request.Path == "/print_qrtext" || request.Path == "/print_with_qr"))
            {
                await _printer.PrintAsync(EscPosTicketBuilder.BuildQrText(request.Body));
                await WriteJson(stream, 200, new { status = "printed" });
                return;
            }

            if (request.Method == "POST" && request.Path == "/print_zpl")
            {
                if (string.IsNullOrWhiteSpace(request.Body))
                {
                    await WriteJson(stream, 400, new { error = "Body vacío. Enviar JSON con campo 'valores'" });
                    return;
                }

                using var zplJson = JsonDocument.Parse(request.Body);
                if (!zplJson.RootElement.TryGetProperty("valores", out var valoresEl))
                {
                    await WriteJson(stream, 400, new { error = "Falta campo 'valores'" });
                    return;
                }

                var zplData = valoresEl.GetString() ?? "";
                await _printer.PrintAsync(EscPosTicketBuilder.BuildRawText(zplData));
                await WriteJson(stream, 200, new { status = "printed" });
                return;
            }

            await WriteJson(stream, 404, new { error = "not-found" });
        }
        catch (JsonException)
        {
            await WriteJson(stream, 400, new { error = "JSON inválido" });
        }
        catch (Exception ex)
        {
            await WriteJson(stream, 500, new { error = ex.Message });
        }
    }

    private static async Task<HttpRequest?> ReadRequest(NetworkStream stream)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        var headerEnd = -1;
        var total = 0;

        while (headerEnd < 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (read <= 0) return null;
            ms.Write(buffer, 0, read);
            total += read;
            headerEnd = IndexOf(ms.GetBuffer(), total, "\r\n\r\n"u8.ToArray());
            if (total > 1024 * 1024) return null;
        }

        var all = ms.ToArray();
        var headerText = Encoding.ASCII.GetString(all, 0, headerEnd);
        var lines = headerText.Split("\r\n");
        var first = lines[0].Split(' ');
        if (first.Length < 2) return null;

        var method = first[0].Trim().ToUpperInvariant();
        var path = first[1].Split('?')[0];
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var idx = line.IndexOf(':');
            if (idx > 0) headers[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }

        var contentLength = headers.TryGetValue("Content-Length", out var cl) && int.TryParse(cl, out var len) ? len : 0;
        var bodyStart = headerEnd + 4;
        var bodyBytes = new MemoryStream();
        if (all.Length > bodyStart)
            bodyBytes.Write(all, bodyStart, all.Length - bodyStart);

        while (bodyBytes.Length < contentLength)
        {
            var remaining = contentLength - (int)bodyBytes.Length;
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)));
            if (read <= 0) break;
            bodyBytes.Write(buffer, 0, read);
        }

        return new HttpRequest(method, path, Encoding.UTF8.GetString(bodyBytes.ToArray()));
    }

    private static int IndexOf(byte[] buffer, int length, byte[] pattern)
    {
        for (var i = 0; i <= length - pattern.Length; i++)
        {
            var ok = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (buffer[i + j] != pattern[j]) { ok = false; break; }
            }
            if (ok) return i;
        }
        return -1;
    }

    private static Task WriteJson(NetworkStream stream, int statusCode, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return WriteRaw(stream, statusCode, json, "application/json; charset=utf-8");
    }

    private static async Task WriteRaw(NetworkStream stream, int statusCode, string body, string contentType)
    {
        var reason = statusCode switch
        {
            200 => "OK",
            204 => "No Content",
            400 => "Bad Request",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "OK"
        };
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = "HTTP/1.1 " + statusCode + " " + reason + "\r\n" +
                     "Access-Control-Allow-Origin: *\r\n" +
                     "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                     "Access-Control-Allow-Headers: Content-Type\r\n" +
                     "Content-Type: " + contentType + "\r\n" +
                     "Content-Length: " + bytes.Length + "\r\n" +
                     "Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
        if (bytes.Length > 0)
            await stream.WriteAsync(bytes);
    }

    private sealed record HttpRequest(string Method, string Path, string Body);
}
