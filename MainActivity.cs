using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using PrintAgentAndroid.Http;
using PrintAgentAndroid.Printing;
using PrintAgentAndroid.Services;
using PrintAgentAndroid.Ui;

namespace PrintAgentAndroid;

[Activity(Label = "PrintAgent Android", MainLauncher = true, Exported = true, Theme = "@android:style/Theme.DeviceDefault.NoActionBar")]
public sealed class MainActivity : Activity
{
    private const string ZplTestPayload = "^XA^FO50,50^ADN,36,20^FDPRINTAGENT TEST^FS^FO50,100^BY2^BCN,80,Y,N,N^FD123456^FS^XZ";
    private const int StatusPollMs = 4000;

    private readonly ServiceConnection _connection = new();
    private readonly Handler _pollHandler = new(Looper.MainLooper!);
    private bool _polling;

    private View? _headerView;
    private TextView? _statusPill;
    private FrameLayout? _circleButton;
    private TextView? _circleLabel;
    private TextView? _hintText;
    private LinearLayout? _statsCard;
    private TextView? _statPortValue;
    private TextView? _statJobsValue;
    private TextView? _statErrorsValue;
    private TextView? _log;

    private ConfigPanelBuilder? _configPanel;
    private TestPanelBuilder? _testPanel;
    private BottomNavBar? _bottomNav;
    private DrawerPanel? _drawer;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        BuildUi();

        RequestNotificationPermissionIfNeeded();

        _connection.LogReceived += Log;
        _connection.Connected += () => RefreshStatus();
        StartAndBindService();

        RefreshStatus();
    }

    private void BuildUi()
    {
        var root = new FrameLayout(this);
        root.SetBackgroundColor(new Color(AppTheme.Background));

        var mainContent = new LinearLayout(this) { Orientation = Orientation.Vertical };
        mainContent.SetPadding(0, 0, 0, AppTheme.DpToPxInt(this, 72));

        _headerView = BuildHeader();
        var headerBaseTop = _headerView.PaddingTop;
        mainContent.AddView(_headerView);
        mainContent.AddView(BuildCenterArea(), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        root.AddView(mainContent, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        _bottomNav = new BottomNavBar(this);
        var navParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { Gravity = GravityFlags.Bottom };
        root.AddView(_bottomNav.Root, navParams);

        _configPanel = new ConfigPanelBuilder(this);
        _testPanel = new TestPanelBuilder(this);
        var logsView = BuildLogsView();
        _drawer = new DrawerPanel(this, _configPanel.Root, logsView, _testPanel.Root);

        var metrics = Resources!.DisplayMetrics!;
        var drawerHeight = (int)(metrics.HeightPixels * 0.65);
        var drawerParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, drawerHeight) { Gravity = GravityFlags.Bottom };
        root.AddView(_drawer.Root, drawerParams);

        root.SetOnApplyWindowInsetsListener(new InsetsListener(_headerView, headerBaseTop, _bottomNav, _drawer));

        WireDrawerAndNav();
        WireConfigPanel();
        WireTestPanel();

        SetContentView(root);
    }

    private View BuildHeader()
    {
        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        header.SetGravity(GravityFlags.CenterVertical);
        var padH = AppTheme.DpToPxInt(this, 20);
        header.SetPadding(padH, AppTheme.DpToPxInt(this, 48), padH, AppTheme.DpToPxInt(this, 16));

        var logo = new ImageView(this);
        logo.SetImageResource(Resource.Drawable.bts_logo);
        logo.SetAdjustViewBounds(true);
        logo.SetScaleType(ImageView.ScaleType.FitCenter);
        var logoSize = AppTheme.DpToPxInt(this, 32);
        var logoParams = new LinearLayout.LayoutParams(logoSize, logoSize) { MarginEnd = AppTheme.DpToPxInt(this, 12) };
        header.AddView(logo, logoParams);

        var titleCol = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var eyebrow = new TextView(this) { Text = "PRINTSERVER", TextSize = 11 };
        eyebrow.SetTextColor(new Color(AppTheme.MutedForeground));
        eyebrow.SetTypeface(eyebrow.Typeface, TypefaceStyle.Bold);
        var title = new TextView(this) { Text = "Panel de control", TextSize = 18 };
        title.SetTextColor(new Color(AppTheme.Foreground));
        title.SetTypeface(title.Typeface, TypefaceStyle.Bold);
        titleCol.AddView(eyebrow);
        titleCol.AddView(title);
        header.AddView(titleCol, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        _statusPill = ViewFactory.CreateStatusPill(this);
        header.AddView(_statusPill);

        return header;
    }

    private View BuildCenterArea()
    {
        var centerArea = new LinearLayout(this) { Orientation = Orientation.Vertical };
        centerArea.SetGravity(GravityFlags.CenterHorizontal);
        centerArea.SetPadding(AppTheme.DpToPxInt(this, 24), 0, AppTheme.DpToPxInt(this, 24), 0);

        var circleSize = AppTheme.DpToPxInt(this, 220);
        _circleButton = new FrameLayout(this) { Clickable = true, Focusable = true };
        _circleButton.Background = ViewFactory.BuildCircleBackground(this, false);

        var circleInner = new LinearLayout(this) { Orientation = Orientation.Vertical };
        circleInner.SetGravity(GravityFlags.Center);
        var circleIcon = new TextView(this) { Text = "🖨", TextSize = 32, Gravity = GravityFlags.Center };
        _circleLabel = new TextView(this) { Text = "SERVIDOR OFF", TextSize = 14, Gravity = GravityFlags.Center };
        _circleLabel.SetTypeface(_circleLabel.Typeface, TypefaceStyle.Bold);
        _circleLabel.SetTextColor(new Color(AppTheme.Foreground));
        circleInner.AddView(circleIcon);
        circleInner.AddView(_circleLabel);
        var innerParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { Gravity = GravityFlags.Center };
        _circleButton.AddView(circleInner, innerParams);

        _circleButton.Touch += (_, e) =>
        {
            switch (e.Event!.Action)
            {
                case MotionEventActions.Down:
                    _circleButton.Animate()!.ScaleX(0.95f).ScaleY(0.95f).SetDuration(100).Start();
                    break;
                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    _circleButton.Animate()!.ScaleX(1f).ScaleY(1f).SetDuration(100).Start();
                    break;
            }
            e.Handled = false;
        };
        _circleButton.Click += (_, _) => ToggleService();

        centerArea.AddView(_circleButton, new LinearLayout.LayoutParams(circleSize, circleSize));

        _hintText = new TextView(this) { Text = "Toca para iniciar el servidor", TextSize = 13 };
        _hintText.SetTextColor(new Color(AppTheme.MutedForeground));
        _hintText.SetPadding(0, AppTheme.DpToPxInt(this, 16), 0, 0);
        centerArea.AddView(_hintText);

        _statsCard = new LinearLayout(this) { Orientation = Orientation.Horizontal, Visibility = ViewStates.Gone };
        var statsPad = AppTheme.DpToPxInt(this, 16);
        _statsCard.SetPadding(statsPad, statsPad, statsPad, statsPad);
        _statsCard.Background = ViewFactory.RoundedBackground(AppTheme.Card, AppTheme.DpToPx(this, 20));

        var (portCol, portValue) = ViewFactory.CreateStatColumn(this, "Puerto", "5000", AppTheme.Foreground);
        _statPortValue = portValue;
        var (jobsCol, jobsValue) = ViewFactory.CreateStatColumn(this, "Trabajos", "0", AppTheme.Foreground);
        _statJobsValue = jobsValue;
        var (errorsCol, errorsValue) = ViewFactory.CreateStatColumn(this, "Errores", "0", AppTheme.Destructive);
        _statErrorsValue = errorsValue;

        var colLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
        _statsCard.AddView(portCol, colLp);
        _statsCard.AddView(BuildSeparator());
        _statsCard.AddView(jobsCol, new LinearLayout.LayoutParams(colLp));
        _statsCard.AddView(BuildSeparator());
        _statsCard.AddView(errorsCol, new LinearLayout.LayoutParams(colLp));

        var statsParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = AppTheme.DpToPxInt(this, 24)
        };
        centerArea.AddView(_statsCard, statsParams);

        return centerArea;
    }

    private View BuildSeparator()
    {
        var sep = new View(this);
        sep.SetBackgroundColor(new Color(AppTheme.Border));
        return sep;
    }

    private View BuildLogsView()
    {
        _log = new TextView(this)
        {
            Text = "Iniciando PrintAgent Android...",
            TextSize = 13
        };
        _log.SetTextColor(new Color(AppTheme.MutedForeground));
        var scroll = new ScrollView(this);
        scroll.AddView(_log);
        return scroll;
    }

    private void WireDrawerAndNav()
    {
        _bottomNav!.OnTabSelected = kind =>
        {
            _drawer!.Toggle(kind);
            _bottomNav.SetActive(_drawer.Current);
            if (_drawer.Current != null) RefreshStatus();
        };
        _drawer!.OnCloseRequested = () => _bottomNav.SetActive(null);
    }

    private void WireConfigPanel()
    {
        _configPanel!.OnUsbRowTap = () => _ = RequestUsbPermissionAsync();
        _configPanel.OnNotificationsRowTap = OnNotificationsRowTapped;
        _configPanel.OnBatteryRowTap = RequestIgnoreBatteryOptimizations;
        _configPanel.OnNetworkToggle = OnNetworkToggled;
        _configPanel.OnAutoStartToggle = SetBootReceiverEnabled;
        _configPanel.OnPrinterSelected = OnPrinterSelected;
    }

    private void OnPrinterSelected(int vendorId, int productId)
    {
        _connection.Service?.Printer?.SetPreferredPrinter(vendorId, productId);
        Log($"Impresora preferida: USB {vendorId}:{productId}");
        RefreshStatus();
    }

    private void WireTestPanel()
    {
        _testPanel!.OnQuickTextTest = () => _ = RunEscPosTestAsync();
        _testPanel.OnQuickZplTest = () => _ = RunZplTestAsync();
        _testPanel.OnSendCustom = (content, isZpl) => _ = SendCustomTestAsync(content, isZpl);
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
        RefreshStatus();
    }

    private void OnNotificationsRowTapped()
    {
        var granted = Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu
            || CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) == Permission.Granted;

        if (granted)
        {
            var intent = new Intent(Settings.ActionApplicationDetailsSettings, Android.Net.Uri.Parse("package:" + PackageName));
            StartActivity(intent);
        }
        else
        {
            RequestNotificationPermissionIfNeeded();
        }
    }

    private void OnNetworkToggled(bool listenAll)
    {
        _connection.Service?.Server?.Reconfigure(listenAll: listenAll);
        Log(listenAll ? "Servidor ahora escucha en la red (LAN)." : "Servidor ahora sólo escucha en localhost.");
        RefreshStatus();
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
            RefreshStatus();
        }
    }

    private ComponentName BootReceiverComponent() =>
        new(PackageName!, Java.Lang.Class.FromType(typeof(BootReceiver)).Name!);

    private bool IsBootReceiverEnabled()
    {
        var state = PackageManager!.GetComponentEnabledSetting(BootReceiverComponent());
        return state != ComponentEnabledState.Disabled;
    }

    private void SetBootReceiverEnabled(bool enabled)
    {
        var newState = enabled ? ComponentEnabledState.Enabled : ComponentEnabledState.Disabled;
        PackageManager!.SetComponentEnabledSetting(BootReceiverComponent(), newState, ComponentEnableOption.DontKillApp);
        Log(enabled ? "Inicio automático activado." : "Inicio automático desactivado.");
        RefreshStatus();
    }

    private async Task RunEscPosTestAsync()
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
    }

    private async Task RunZplTestAsync()
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
    }

    private async Task SendCustomTestAsync(string content, bool isZpl)
    {
        var printer = _connection.Service?.Printer;
        if (printer == null) { Log("Servicio aún no conectado."); return; }
        try
        {
            var bytes = isZpl
                ? System.Text.Encoding.UTF8.GetBytes(content)
                : EscPosTicketBuilder.BuildRawText(content);
            await printer.PrintAsync(bytes);
            Log($"Prueba personalizada ({(isZpl ? "ZPL" : "Texto")}) impresa.");
            _testPanel!.ShowLastSent(content, isZpl);
            _testPanel.ClearCustomInput();
        }
        catch (Exception ex)
        {
            Log("Error en prueba personalizada: " + ex.Message);
        }
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
            _connection.Clear();
            Log("Servicio detenido manualmente.");
        }
        else
        {
            StartAndBindService();
            Log("Iniciando servicio...");
        }
        RefreshStatus();
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
        var server = service?.Server;
        var running = printer != null && server != null;

        UpdateCircleAndPill(running);
        UpdateStats(running, server);
        UpdateConfigPanel(printer, server);
    }

    private void UpdateCircleAndPill(bool running)
    {
        RunOnUiThread(() =>
        {
            if (_circleButton == null) return;
            _circleButton.Background = ViewFactory.BuildCircleBackground(this, running);
            _circleButton.Elevation = AppTheme.DpToPx(this, running ? 16 : 4);
            _circleLabel!.Text = running ? "SERVIDOR ON" : "SERVIDOR OFF";
            _hintText!.Text = running ? "Toca para detener el servidor" : "Toca para iniciar el servidor";
            ViewFactory.ApplyStatusPill(_statusPill!, running);
        });
    }

    private void UpdateStats(bool running, LocalHttpServer? server)
    {
        RunOnUiThread(() =>
        {
            if (_statsCard == null) return;
            _statsCard.Visibility = running ? ViewStates.Visible : ViewStates.Gone;
            if (server == null) return;
            _statPortValue!.Text = server.Port.ToString();
            _statJobsValue!.Text = server.JobsCompleted.ToString();
            _statErrorsValue!.Text = server.JobsFailed.ToString();
        });
    }

    private void UpdateConfigPanel(UsbEscPosPrinter? printer, LocalHttpServer? server)
    {
        var device = printer?.FindPrinterDevice();
        var usbGranted = device != null && printer!.HasPermission(device);
        var notifGranted = Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu
            || CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) == Permission.Granted;
        var autoStartEnabled = IsBootReceiverEnabled();
        var batteryExempt = IsIgnoringBatteryOptimizations();
        var printers = printer?.ListDevicesDetailed()
            .Select(p => (p.FriendlyName, p.VendorId, p.ProductId, p.HasPermission, p.IsSelected))
            .ToList() ?? new List<(string, int, int, bool, bool)>();

        RunOnUiThread(() =>
        {
            if (_configPanel == null) return;
            _configPanel.SetUsbState(usbGranted);
            _configPanel.SetNotificationsState(notifGranted);
            _configPanel.SetBatteryState(batteryExempt);
            _configPanel.SetNetworkState(server?.BindAddress == "0.0.0.0");
            _configPanel.SetAutoStartState(autoStartEnabled);
            _configPanel.SetPort(server?.Port ?? 5000);
            _configPanel.SetPrinters(printers);
        });
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

        public void Clear() => Service = null;
    }

    private sealed class InsetsListener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        private readonly View _header;
        private readonly int _headerBaseTop;
        private readonly BottomNavBar _bottomNav;
        private readonly DrawerPanel _drawer;

        public InsetsListener(View header, int headerBaseTop, BottomNavBar bottomNav, DrawerPanel drawer)
        {
            _header = header;
            _headerBaseTop = headerBaseTop;
            _bottomNav = bottomNav;
            _drawer = drawer;
        }

        public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
        {
            _header.SetPadding(_header.PaddingLeft, _headerBaseTop + insets.SystemWindowInsetTop, _header.PaddingRight, _header.PaddingBottom);
            _bottomNav.ApplyBottomInset(insets.SystemWindowInsetBottom);
            _drawer.ApplyBottomInset(insets.SystemWindowInsetBottom);
            return insets;
        }
    }
}
