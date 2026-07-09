using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
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

    private TextView? _permissionsBadge;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        BuildUi();

        RequestNotificationPermissionIfNeeded();

        _connection.LogReceived += Log;
        _connection.Connected += () =>
        {
            if (_btnToggleService != null) _btnToggleService.Text = "Detener servicio";
            RefreshStatus();
        };
        StartAndBindService();

        RefreshStatus();
    }

    private void BuildUi()
    {
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
        _statusBadge.SetPadding(0, 16, 0, 4);
        root.AddView(_statusBadge);

        _permissionsBadge = new TextView(this) { TextSize = 13 };
        _permissionsBadge.SetPadding(0, 0, 0, 16);
        root.AddView(_permissionsBadge);

        _btnToggleService = new Button(this) { Text = "Detener servicio" };
        var btnStatus = new Button(this) { Text = "Ver estado" };
        var btnGrantAll = new Button(this) { Text = "Conceder todos los permisos" };
        var btnPermission = new Button(this) { Text = "Pedir permiso USB" };
        var btnBattery = new Button(this) { Text = "Permitir ejecución en segundo plano" };
        var btnTestEscPos = new Button(this) { Text = "Test térmico (ESC/POS)" };
        var btnTestZpl = new Button(this) { Text = "Test etiqueta (ZPL)" };

        root.AddView(_btnToggleService);
        root.AddView(btnStatus);
        root.AddView(btnGrantAll);
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

        _btnToggleService.Click += (_, _) => ToggleService();
        btnStatus.Click += (_, _) => RefreshStatus();
        btnGrantAll.Click += async (_, _) => await GrantAllPermissionsAsync();
        btnBattery.Click += (_, _) => RequestIgnoreBatteryOptimizations();
        btnPermission.Click += async (_, _) => await RequestUsbPermissionAsync();
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
    }

    private void RequestNotificationPermissionIfNeeded()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return;

        if (CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) == Permission.Granted)
        {
            Log("Permiso de notificaciones ya concedido.");
            return;
        }

        RequestPermissions(new[] { global::Android.Manifest.Permission.PostNotifications }, 100);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
        Log(granted ? "Permiso de notificaciones concedido." : "Permiso de notificaciones denegado.");
        UpdatePermissionsBadge();
    }

    private async Task RequestUsbPermissionAsync()
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
        finally
        {
            UpdatePermissionsBadge();
        }
    }

    private async Task GrantAllPermissionsAsync()
    {
        RequestNotificationPermissionIfNeeded();
        RequestIgnoreBatteryOptimizations();
        await RequestUsbPermissionAsync();
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
            var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations, Android.Net.Uri.Parse("package:" + PackageName));
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
            UpdatePermissionsBadge();
            return;
        }

        var device = printer.FindPrinterDevice();
        UpdateBadge(running: true, device, printer);
        UpdatePermissionsBadge();
    }

    private void UpdatePermissionsBadge()
    {
        if (_permissionsBadge == null) return;

        var notifStatus = Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu
            ? "no aplica"
            : CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) == Permission.Granted ? "sí" : "no";

        var batteryStatus = IsIgnoringBatteryOptimizations() ? "sí" : "no";

        var printer = _connection.Service?.Printer;
        var device = printer?.FindPrinterDevice();
        var usbStatus = device == null ? "sin impresora detectada" : printer!.HasPermission(device) ? "sí" : "no";

        var text = $"Permisos — notificaciones: {notifStatus} | batería exenta: {batteryStatus} | USB: {usbStatus}";
        RunOnUiThread(() => _permissionsBadge.Text = text);
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
