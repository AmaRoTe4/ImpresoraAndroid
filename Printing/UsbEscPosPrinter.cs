using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;

namespace PrintAgentAndroid.Printing;

public sealed class UsbEscPosPrinter : IDisposable
{
    private readonly Context _context;
    private readonly UsbManager _usbManager;
    private readonly Action<string> _log;
    private readonly string _permissionAction;
    private PermissionReceiver? _receiver;
    private TaskCompletionSource<bool>? _permissionTcs;

    public UsbEscPosPrinter(Context context, Action<string> log)
    {
        _context = context;
        _log = log;
        _usbManager = (UsbManager)context.GetSystemService(Context.UsbService)!;
        _permissionAction = context.PackageName + ".USB_PERMISSION";
        RegisterReceiver();
    }

    public IReadOnlyList<object> ListDevices()
    {
        return (_usbManager.DeviceList?.Values ?? Enumerable.Empty<UsbDevice>())
            .Select(d => new
            {
                name = d.DeviceName,
                vendorId = d.VendorId,
                productId = d.ProductId,
                hasPermission = HasPermission(d),
                interfaces = d.InterfaceCount
            })
            .Cast<object>()
            .ToList();
    }

    public UsbDevice? FindPrinterDevice()
    {
        foreach (var device in _usbManager.DeviceList?.Values ?? Enumerable.Empty<UsbDevice>())
        {
            if (TryFindBulkOutEndpoint(device, out _, out _))
                return device;
        }
        return null;
    }

    public bool HasPermission(UsbDevice device) => _usbManager.HasPermission(device);

    public async Task EnsurePermissionAsync()
    {
        var device = FindPrinterDevice() ?? throw new InvalidOperationException("No se detectó ninguna impresora USB con endpoint BULK OUT.");
        if (HasPermission(device)) return;

        _permissionTcs = new TaskCompletionSource<bool>();

        var intent = new Intent(_permissionAction).SetPackage(_context.PackageName);
        var flags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            flags |= PendingIntentFlags.Immutable;

        var pendingIntent = PendingIntent.GetBroadcast(_context, 0, intent, flags);
        _usbManager.RequestPermission(device, pendingIntent);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var _ = timeout.Token.Register(() => _permissionTcs.TrySetException(new TimeoutException("Timeout esperando permiso USB.")));
        await _permissionTcs.Task;
    }

    public async Task PrintAsync(byte[] bytes)
    {
        var device = FindPrinterDevice() ?? throw new InvalidOperationException("Impresora USB no detectada.");
        if (!HasPermission(device))
            await EnsurePermissionAsync();

        if (!TryFindBulkOutEndpoint(device, out var usbInterface, out var endpoint))
            throw new InvalidOperationException("No se encontró endpoint BULK OUT en la impresora.");

        using var connection = _usbManager.OpenDevice(device) ?? throw new InvalidOperationException("No se pudo abrir conexión USB.");
        if (!connection.ClaimInterface(usbInterface, true))
            throw new InvalidOperationException("No se pudo tomar control de la interfaz USB.");

        try
        {
            const int chunkSize = 16 * 1024;
            var offset = 0;
            while (offset < bytes.Length)
            {
                var count = Math.Min(chunkSize, bytes.Length - offset);
                var chunk = new byte[count];
                Buffer.BlockCopy(bytes, offset, chunk, 0, count);

                var written = connection.BulkTransfer(endpoint, chunk, count, timeout: 5000);
                if (written <= 0)
                    throw new IOException($"Falló BulkTransfer USB. Resultado: {written}");
                offset += written;
            }
        }
        finally
        {
            connection.ReleaseInterface(usbInterface);
        }
    }

    private static bool TryFindBulkOutEndpoint(UsbDevice device, out UsbInterface usbInterface, out UsbEndpoint endpoint)
    {
        for (var i = 0; i < device.InterfaceCount; i++)
        {
            var candidateInterface = device.GetInterface(i)!;
            for (var e = 0; e < candidateInterface.EndpointCount; e++)
            {
                var candidateEndpoint = candidateInterface.GetEndpoint(e)!;
                if (candidateEndpoint.Direction == UsbAddressing.Out && candidateEndpoint.Type == UsbAddressing.XferBulk)
                {
                    usbInterface = candidateInterface;
                    endpoint = candidateEndpoint;
                    return true;
                }
            }
        }

        usbInterface = null!;
        endpoint = null!;
        return false;
    }

    private void RegisterReceiver()
    {
        _receiver = new PermissionReceiver(_permissionAction, granted =>
        {
            if (granted)
            {
                _log("Permiso USB concedido.");
                _permissionTcs?.TrySetResult(true);
            }
            else
            {
                _log("Permiso USB denegado.");
                _permissionTcs?.TrySetException(new UnauthorizedAccessException("Permiso USB denegado."));
            }
        });

        var filter = new IntentFilter(_permissionAction);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            _context.RegisterReceiver(_receiver, filter, ReceiverFlags.NotExported);
        else
            _context.RegisterReceiver(_receiver, filter);
    }

    public void Dispose()
    {
        if (_receiver != null)
        {
            try { _context.UnregisterReceiver(_receiver); } catch { }
            _receiver = null;
        }
    }

    private sealed class PermissionReceiver : BroadcastReceiver
    {
        private readonly string _action;
        private readonly Action<bool> _callback;

        public PermissionReceiver(string action, Action<bool> callback)
        {
            _action = action;
            _callback = callback;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action != _action) return;
            var granted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
            _callback(granted);
        }
    }
}
