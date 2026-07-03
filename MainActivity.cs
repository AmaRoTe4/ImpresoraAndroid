using Android.App;
using Android.OS;
using Android.Widget;
using PrintAgentAndroid.Http;
using PrintAgentAndroid.Printing;

namespace PrintAgentAndroid;

[Activity(Label = "PrintAgent Android", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private TextView? _status;
    private UsbEscPosPrinter? _printer;
    private LocalHttpServer? _server;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _status = new TextView(this)
        {
            Text = "Iniciando PrintAgent Android...",
            TextSize = 16
        };

        var btnStatus = new Button(this) { Text = "Ver estado" };
        var btnPermission = new Button(this) { Text = "Pedir permiso USB" };
        var btnTest = new Button(this) { Text = "Imprimir prueba" };

        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            Padding = 24
        };
        layout.AddView(_status);
        layout.AddView(btnStatus);
        layout.AddView(btnPermission);
        layout.AddView(btnTest);
        SetContentView(layout);

        _printer = new UsbEscPosPrinter(this, Log);
        _server = new LocalHttpServer(_printer, Log, port: 5000);
        _ = _server.StartAsync();

        btnStatus.Click += (_, _) => RefreshStatus();
        btnPermission.Click += async (_, _) =>
        {
            try
            {
                await _printer.EnsurePermissionAsync();
                Log("Permiso USB OK.");
            }
            catch (Exception ex)
            {
                Log("Error permiso USB: " + ex.Message);
            }
        };
        btnTest.Click += async (_, _) =>
        {
            try
            {
                var bytes = EscPosTicketBuilder.BuildRawText("TEST PRINTAGENT ANDROID\nPuerto: http://127.0.0.1:5000\n");
                await _printer.PrintAsync(bytes);
                Log("Prueba impresa.");
            }
            catch (Exception ex)
            {
                Log("Error imprimiendo: " + ex.Message);
            }
        };

        RefreshStatus();
    }

    protected override void OnDestroy()
    {
        _server?.Stop();
        _printer?.Dispose();
        base.OnDestroy();
    }

    private void RefreshStatus()
    {
        if (_printer == null)
        {
            Log("Printer no inicializada.");
            return;
        }

        var device = _printer.FindPrinterDevice();
        Log(device == null
            ? "Servidor activo en http://127.0.0.1:5000 - Impresora USB no detectada."
            : $"Servidor activo en http://127.0.0.1:5000 - USB detectado: {device.DeviceName} VID:{device.VendorId} PID:{device.ProductId} Permiso:{_printer.HasPermission(device)}");
    }

    private void Log(string message)
    {
        RunOnUiThread(() =>
        {
            if (_status != null)
                _status.Text = $"{DateTime.Now:HH:mm:ss} - {message}\n\n" + _status.Text;
        });
    }
}
