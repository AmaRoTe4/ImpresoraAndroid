using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using PrintAgentAndroid.Printing;
using PrintAgentAndroid.Services;

namespace PrintAgentAndroid;

[Activity(Label = "PrintAgent Android", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private const string ZplTestPayload = "^XA^FO50,50^ADN,36,20^FDPRINTAGENT TEST^FS^FO50,100^BY2^BCN,80,Y,N,N^FD123456^FS^XZ";

    private TextView? _status;
    private readonly ServiceConnection _connection = new();

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
        var btnTestEscPos = new Button(this) { Text = "Imprimir prueba (ESC/POS ticket)" };
        var btnTestZpl = new Button(this) { Text = "Imprimir prueba (ZPL etiqueta)" };

        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        layout.SetPadding(24, 24, 24, 24);
        layout.AddView(_status);
        layout.AddView(btnStatus);
        layout.AddView(btnPermission);
        layout.AddView(btnTestEscPos);
        layout.AddView(btnTestZpl);
        SetContentView(layout);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            RequestPermissions(new[] { global::Android.Manifest.Permission.PostNotifications }, 100);

        _connection.LogReceived += Log;
        StartAndBindService();

        btnStatus.Click += (_, _) => RefreshStatus();
        btnPermission.Click += async (_, _) =>
        {
            var printer = _connection.Service?.Printer;
            if (printer == null) { Log("Servicio aún no conectado."); return; }
            try
            {
                await printer.EnsurePermissionAsync();
                Log("Permiso USB OK.");
            }
            catch (Exception ex)
            {
                Log("Error permiso USB: " + ex.Message);
            }
        };
        btnTestEscPos.Click += async (_, _) =>
        {
            var printer = _connection.Service?.Printer;
            if (printer == null) { Log("Servicio aún no conectado."); return; }
            try
            {
                var bytes = EscPosTicketBuilder.BuildRawText("TEST PRINTAGENT ANDROID (ESC/POS)\nServidor: puerto 5000\n");
                await printer.PrintAsync(bytes);
                Log("Prueba ESC/POS impresa.");
            }
            catch (Exception ex)
            {
                Log("Error imprimiendo ESC/POS: " + ex.Message);
            }
        };
        btnTestZpl.Click += async (_, _) =>
        {
            var printer = _connection.Service?.Printer;
            if (printer == null) { Log("Servicio aún no conectado."); return; }
            try
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(ZplTestPayload);
                await printer.PrintAsync(bytes);
                Log("Prueba ZPL impresa.");
            }
            catch (Exception ex)
            {
                Log("Error imprimiendo ZPL: " + ex.Message);
            }
        };

        RefreshStatus();
    }

    private void StartAndBindService()
    {
        var intent = new Intent(this, typeof(PrintAgentService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            StartForegroundService(intent);
        else
            StartService(intent);

        BindService(intent, _connection, Bind.AutoCreate);
    }

    protected override void OnDestroy()
    {
        try { UnbindService(_connection); } catch { }
        base.OnDestroy();
    }

    private void RefreshStatus()
    {
        var printer = _connection.Service?.Printer;
        if (printer == null)
        {
            Log("Servicio aún no conectado.");
            return;
        }

        var device = printer.FindPrinterDevice();
        Log(device == null
            ? "Servidor activo en puerto 5000 - Impresora USB no detectada."
            : $"Servidor activo en puerto 5000 - USB detectado: {device.DeviceName} VID:{device.VendorId} PID:{device.ProductId} Permiso:{printer.HasPermission(device)}");
    }

    private void Log(string message)
    {
        RunOnUiThread(() =>
        {
            if (_status != null)
                _status.Text = $"{DateTime.Now:HH:mm:ss} - {message}\n\n" + _status.Text;
        });
    }

    private sealed class ServiceConnection : Java.Lang.Object, IServiceConnection
    {
        public PrintAgentService? Service { get; private set; }
        public event Action<string>? LogReceived;

        public void OnServiceConnected(ComponentName? name, IBinder? service)
        {
            Service = ((PrintAgentService.LocalBinder)service!).Service;
            Service.LogEmitted += msg => LogReceived?.Invoke(msg);
            LogReceived?.Invoke("Servicio conectado.");
        }

        public void OnServiceDisconnected(ComponentName? name) => Service = null;
    }
}
