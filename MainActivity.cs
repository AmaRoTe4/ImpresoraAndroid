using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using PrintAgentAndroid.Printing;
using PrintAgentAndroid.Services;

namespace PrintAgentAndroid;

[Activity(Label = "PrintAgent Android", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private const string ZplTestPayload = "^XA^FO50,50^ADN,36,20^FDPRINTAGENT TEST^FS^FO50,100^BY2^BCN,80,Y,N,N^FD123456^FS^XZ";
    private const int StatusPollMs = 4000;

    private TextView? _statusBadge;
    private TextView? _log;
    private Button? _btnToggleService;
    private readonly ServiceConnection _connection = new();
    private readonly Handler _pollHandler = new(Looper.MainLooper!);
    private bool _polling;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(24, 24, 24, 24);

        var logo = new ImageView(this);
        logo.SetImageResource(Resource.Drawable.bts_logo);
        logo.SetAdjustViewBounds(true);
        logo.SetScaleType(ImageView.ScaleType.FitCenter);
        logo.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 220);
        root.AddView(logo);

        _statusBadge = new TextView(this)
        {
            Text = "Estado: iniciando...",
            TextSize = 15
        };
        _statusBadge.SetPadding(0, 16, 0, 16);
        root.AddView(_statusBadge);

        _btnToggleService = new Button(this) { Text = "Detener servicio" };
        var btnStatus = new Button(this) { Text = "Ver estado" };
        var btnPermission = new Button(this) { Text = "Pedir permiso USB" };
        var btnBattery = new Button(this) { Text = "Permitir ejecución en segundo plano" };
        var btnTestEscPos = new Button(this) { Text = "Imprimir prueba (ESC/POS ticket)" };
        var btnTestZpl = new Button(this) { Text = "Imprimir prueba (ZPL etiqueta)" };

        root.AddView(_btnToggleService);
        root.AddView(btnStatus);
        root.AddView(btnPermission);
        root.AddView(btnBattery);
        root.AddView(btnTestEscPos);
        root.AddView(btnTestZpl);

        var logHeader = new TextView(this) { Text = "Registro de actividad", TextSize = 14 };
        logHeader.SetPadding(0, 24, 0, 8);
        logHeader.SetTypeface(logHeader.Typeface, TypefaceStyle.Bold);
        root.AddView(logHeader);

        _log = new TextView(this)
        {
            Text = "Iniciando PrintAgent Android...",
            TextSize = 13
        };
        var scroll = new ScrollView(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f)
        };
        scroll.AddView(_log);
        root.AddView(scroll);

        SetContentView(root);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            RequestPermissions(new[] { global::Android.Manifest.Permission.PostNotifications }, 100);

        _connection.LogReceived += Log;
        _connection.Connected += () =>
        {
            if (_btnToggleService != null) _btnToggleService.Text = "Detener servicio";
            RefreshStatus();
        };
        StartAndBindService();

        _btnToggleService.Click += (_, _) => ToggleService();
        btnStatus.Click += (_, _) => RefreshStatus();
        btnBattery.Click += (_, _) => RequestIgnoreBatteryOptimizations();
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

    private bool IsIgnoringBatteryOptimizations()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.M) return true;
        var pm = (PowerManager?)GetSystemService(PowerService);
        return pm?.IsIgnoringBatteryOptimizations(PackageName) ?? false;
    }

    private void RequestIgnoreBatteryOptimizations()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.M)
        {
            Log("Este Android no tiene optimización de batería que pedir excepción.");
            return;
        }

        if (IsIgnoringBatteryOptimizations())
        {
            Log("Ya está exenta de la optimización de batería.");
            return;
        }

        try
        {
            var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations, Uri.Parse("package:" + PackageName));
            StartActivity(intent);
        }
        catch (Exception ex)
        {
            Log("No se pudo abrir el diálogo de optimización de batería: " + ex.Message);
        }
    }

    private void ToggleService()
    {
        if (_connection.Service != null)
        {
            try { UnbindService(_connection); } catch { }
            StopService(new Intent(this, typeof(PrintAgentService)));
            if (_btnToggleService != null) _btnToggleService.Text = "Iniciar servicio";
            Log("Servicio detenido manualmente.");
            UpdateBadge(running: false);
        }
        else
        {
            StartAndBindService();
            Log("Iniciando servicio...");
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        _polling = true;
        _pollHandler.PostDelayed(PollStatus, StatusPollMs);
    }

    protected override void OnPause()
    {
        _polling = false;
        _pollHandler.RemoveCallbacksAndMessages(null);
        base.OnPause();
    }

    private void PollStatus()
    {
        if (!_polling) return;
        RefreshStatus();
        _pollHandler.PostDelayed(PollStatus, StatusPollMs);
    }

    protected override void OnDestroy()
    {
        _pollHandler.RemoveCallbacksAndMessages(null);
        try { UnbindService(_connection); } catch { }
        base.OnDestroy();
    }

    private void RefreshStatus()
    {
        var service = _connection.Service;
        var printer = service?.Printer;
        var serverUp = service?.Server != null;

        if (printer == null || !serverUp)
        {
            UpdateBadge(running: false);
            return;
        }

        var device = printer.FindPrinterDevice();
        UpdateBadge(running: true, device, printer);
    }

    private void UpdateBadge(bool running, global::Android.Hardware.Usb.UsbDevice? device = null, UsbEscPosPrinter? printer = null)
    {
        if (_statusBadge == null) return;

        var batteryText = IsIgnoringBatteryOptimizations() ? "batería: exenta" : "batería: SIN exención (puede matar el servicio)";

        var text = !running
            ? $"Estado: SERVICIO DETENIDO — {batteryText}"
            : device == null
                ? $"Estado: servidor activo (puerto 5000) — impresora USB no detectada — {batteryText}"
                : $"Estado: servidor activo (puerto 5000) — USB VID:{device.VendorId} PID:{device.ProductId} — permiso:{(printer!.HasPermission(device) ? "sí" : "no")} — {batteryText}";

        RunOnUiThread(() => _statusBadge.Text = text);
    }

    private void Log(string message)
    {
        RunOnUiThread(() =>
        {
            if (_log != null)
                _log.Text = $"{DateTime.Now:HH:mm:ss} - {message}\n\n" + _log.Text;
        });
    }

    private sealed class ServiceConnection : Java.Lang.Object, IServiceConnection
    {
        public PrintAgentService? Service { get; private set; }
        public event Action<string>? LogReceived;
        public event Action? Connected;

        public void OnServiceConnected(ComponentName? name, IBinder? service)
        {
            Service = ((PrintAgentService.LocalBinder)service!).Service;
            Service.LogEmitted += msg => LogReceived?.Invoke(msg);
            LogReceived?.Invoke("Servicio conectado.");
            Connected?.Invoke();
        }

        public void OnServiceDisconnected(ComponentName? name) => Service = null;
    }
}
