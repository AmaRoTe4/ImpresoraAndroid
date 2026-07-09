using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace PrintAgentAndroid.Ui;

public sealed class ConfigPanelBuilder
{
    public View Root { get; }

    public Action? OnUsbRowTap;
    public Action? OnNotificationsRowTap;
    public Action? OnBatteryRowTap;
    public Action<bool>? OnNetworkToggle;
    public Action<bool>? OnAutoStartToggle;

    private readonly Context _context;
    private readonly TextView _usbStatus;
    private readonly TextView _notifStatus;
    private readonly TextView _batteryStatus;
    private readonly Switch _networkSwitch;
    private readonly Switch _autoStartSwitch;
    private readonly TextView _portValue;
    private bool _suppress;

    public ConfigPanelBuilder(Context context)
    {
        _context = context;
        var scroll = new ScrollView(context);
        var column = new LinearLayout(context) { Orientation = Orientation.Vertical };
        column.SetPadding(0, 0, 0, AppTheme.DpToPxInt(context, 24));

        var (usbRow, usbStatus) = BuildInfoRow("Acceso USB", "Permitir impresión por USB");
        _usbStatus = usbStatus;
        usbRow.Click += (_, _) => OnUsbRowTap?.Invoke();
        column.AddView(usbRow, RowParams(context, 0));

        var (networkRow, networkSwitch) = BuildSwitchRow("Acceso de red", "Aceptar conexiones desde la red (LAN)");
        _networkSwitch = networkSwitch;
        _networkSwitch.CheckedChange += (_, e) =>
        {
            if (!_suppress) OnNetworkToggle?.Invoke(e.IsChecked);
        };
        column.AddView(networkRow, RowParams(context, 12));

        var (notifRow, notifStatus) = BuildInfoRow("Notificaciones", "Alertas de trabajos y errores");
        _notifStatus = notifStatus;
        notifRow.Click += (_, _) => OnNotificationsRowTap?.Invoke();
        column.AddView(notifRow, RowParams(context, 12));

        var (autoStartRow, autoStartSwitch) = BuildSwitchRow("Inicio automático", "Iniciar al arrancar el sistema");
        _autoStartSwitch = autoStartSwitch;
        _autoStartSwitch.CheckedChange += (_, e) =>
        {
            if (!_suppress) OnAutoStartToggle?.Invoke(e.IsChecked);
        };
        column.AddView(autoStartRow, RowParams(context, 12));

        var (batteryRow, batteryStatus) = BuildInfoRow("Optimización de batería", "Evitar que el sistema mate el servicio");
        _batteryStatus = batteryStatus;
        batteryRow.Click += (_, _) => OnBatteryRowTap?.Invoke();
        column.AddView(batteryRow, RowParams(context, 12));

        var portRow = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        portRow.SetGravity(GravityFlags.CenterVertical);
        var pad = AppTheme.DpToPxInt(context, 16);
        portRow.SetPadding(pad, pad, pad, pad);
        portRow.Background = ViewFactory.RoundedBackground(AppTheme.Muted, AppTheme.DpToPx(context, 16));

        var portLabel = new TextView(context) { Text = "Puerto de escucha", TextSize = 14 };
        portLabel.SetTextColor(new Color(AppTheme.Foreground));
        portRow.AddView(portLabel, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        _portValue = new TextView(context) { TextSize = 14 };
        _portValue.SetTypeface(Typeface.Monospace, TypefaceStyle.Bold);
        _portValue.SetTextColor(new Color(AppTheme.Accent));
        portRow.AddView(_portValue);
        column.AddView(portRow, RowParams(context, 12));

        scroll.AddView(column);
        Root = scroll;
    }

    public void SetUsbState(bool granted)
    {
        _usbStatus.Text = granted ? "Concedido" : "Toca para conceder";
        _usbStatus.SetTextColor(new Color(granted ? AppTheme.Accent : AppTheme.MutedForeground));
    }

    public void SetNotificationsState(bool granted)
    {
        _notifStatus.Text = granted ? "Concedido" : "Toca para conceder";
        _notifStatus.SetTextColor(new Color(granted ? AppTheme.Accent : AppTheme.MutedForeground));
    }

    public void SetBatteryState(bool exempt)
    {
        _batteryStatus.Text = exempt ? "Exenta" : "Toca para conceder";
        _batteryStatus.SetTextColor(new Color(exempt ? AppTheme.Accent : AppTheme.MutedForeground));
    }

    public void SetNetworkState(bool listenAll)
    {
        _suppress = true;
        _networkSwitch.Checked = listenAll;
        _suppress = false;
    }

    public void SetAutoStartState(bool enabled)
    {
        _suppress = true;
        _autoStartSwitch.Checked = enabled;
        _suppress = false;
    }

    public void SetPort(int port) => _portValue.Text = port.ToString();

    private static LinearLayout.LayoutParams RowParams(Context context, int marginTopDp) =>
        new(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = AppTheme.DpToPxInt(context, marginTopDp) };

    private (LinearLayout row, TextView status) BuildInfoRow(string label, string desc)
    {
        var row = new LinearLayout(_context) { Orientation = Orientation.Horizontal, Clickable = true, Focusable = true };
        row.SetGravity(GravityFlags.CenterVertical);
        var pad = AppTheme.DpToPxInt(_context, 16);
        row.SetPadding(pad, pad, pad, pad);
        row.Background = ViewFactory.RoundedBackground(AppTheme.Muted, AppTheme.DpToPx(_context, 16));

        var textCol = new LinearLayout(_context) { Orientation = Orientation.Vertical };
        var lbl = new TextView(_context) { Text = label, TextSize = 14 };
        lbl.SetTextColor(new Color(AppTheme.Foreground));
        var descTv = new TextView(_context) { Text = desc, TextSize = 12 };
        descTv.SetTextColor(new Color(AppTheme.MutedForeground));
        textCol.AddView(lbl);
        textCol.AddView(descTv);
        row.AddView(textCol, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        var status = new TextView(_context) { TextSize = 12 };
        row.AddView(status);
        return (row, status);
    }

    private (LinearLayout row, Switch sw) BuildSwitchRow(string label, string desc)
    {
        var row = new LinearLayout(_context) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        var pad = AppTheme.DpToPxInt(_context, 16);
        row.SetPadding(pad, pad, pad, pad);
        row.Background = ViewFactory.RoundedBackground(AppTheme.Muted, AppTheme.DpToPx(_context, 16));

        var textCol = new LinearLayout(_context) { Orientation = Orientation.Vertical };
        var lbl = new TextView(_context) { Text = label, TextSize = 14 };
        lbl.SetTextColor(new Color(AppTheme.Foreground));
        var descTv = new TextView(_context) { Text = desc, TextSize = 12 };
        descTv.SetTextColor(new Color(AppTheme.MutedForeground));
        textCol.AddView(lbl);
        textCol.AddView(descTv);
        row.AddView(textCol, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        var sw = ViewFactory.CreateTintedSwitch(_context);
        row.AddView(sw);
        return (row, sw);
    }
}
