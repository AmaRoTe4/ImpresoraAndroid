using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;

namespace PrintAgentAndroid.Ui;

public enum PanelKind
{
    Config,
    Logs,
    Test
}

public sealed class DrawerPanel
{
    public FrameLayout Root { get; }
    public Action? OnCloseRequested;
    public PanelKind? Current => _current;

    private readonly TextView _title;
    private readonly View _configView;
    private readonly View _logsView;
    private readonly View _testView;
    private PanelKind? _current;

    public DrawerPanel(Context context, View configView, View logsView, View testView)
    {
        _configView = configView;
        _logsView = logsView;
        _testView = testView;

        Root = new FrameLayout(context) { Visibility = ViewStates.Gone };
        var r = Theme.DpToPx(context, 24);
        var bg = new GradientDrawable();
        bg.SetShape(ShapeType.Rectangle);
        bg.SetColor(new Color(Theme.Card));
        bg.SetCornerRadii(new[] { r, r, r, r, 0f, 0f, 0f, 0f });
        Root.Background = bg;

        var column = new LinearLayout(context) { Orientation = Orientation.Vertical };

        var handleRow = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        handleRow.SetGravity(GravityFlags.CenterHorizontal);
        var handle = new View(context);
        var handleBg = new GradientDrawable();
        handleBg.SetShape(ShapeType.Rectangle);
        handleBg.SetCornerRadius(Theme.DpToPx(context, 2));
        handleBg.SetColor(new Color(Theme.Border));
        handle.Background = handleBg;
        var handleParams = new LinearLayout.LayoutParams(Theme.DpToPxInt(context, 40), Theme.DpToPxInt(context, 4))
        {
            TopMargin = Theme.DpToPxInt(context, 10),
            BottomMargin = Theme.DpToPxInt(context, 8)
        };
        handleRow.AddView(handle, handleParams);
        column.AddView(handleRow);

        var headerRow = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        headerRow.SetGravity(GravityFlags.CenterVertical);
        var padH = Theme.DpToPxInt(context, 20);
        headerRow.SetPadding(padH, 0, padH, Theme.DpToPxInt(context, 12));

        _title = new TextView(context) { TextSize = 16 };
        _title.SetTextColor(new Color(Theme.Foreground));
        _title.SetTypeface(_title.Typeface, TypefaceStyle.Bold);
        headerRow.AddView(_title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        var closePad = Theme.DpToPxInt(context, 8);
        var closeBtn = new TextView(context) { Text = "✕", TextSize = 14, Clickable = true, Focusable = true };
        closeBtn.SetTextColor(new Color(Theme.MutedForeground));
        closeBtn.SetPadding(closePad, closePad, closePad, closePad);
        closeBtn.Background = ViewFactory.CirclePillBackground(Theme.Muted);
        closeBtn.Click += (_, _) =>
        {
            Hide();
            OnCloseRequested?.Invoke();
        };
        headerRow.AddView(closeBtn);
        column.AddView(headerRow);

        var contentContainer = new FrameLayout(context);
        contentContainer.AddView(_configView);
        contentContainer.AddView(_logsView);
        contentContainer.AddView(_testView);
        column.AddView(contentContainer, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        Root.AddView(column, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
    }

    public void Show(PanelKind kind)
    {
        _current = kind;
        Root.Visibility = ViewStates.Visible;
        _title.Text = kind switch
        {
            PanelKind.Config => "Configuración",
            PanelKind.Logs => "Registros",
            _ => "Prueba de impresión"
        };
        _configView.Visibility = kind == PanelKind.Config ? ViewStates.Visible : ViewStates.Gone;
        _logsView.Visibility = kind == PanelKind.Logs ? ViewStates.Visible : ViewStates.Gone;
        _testView.Visibility = kind == PanelKind.Test ? ViewStates.Visible : ViewStates.Gone;
    }

    public void Hide()
    {
        _current = null;
        Root.Visibility = ViewStates.Gone;
    }

    public void Toggle(PanelKind kind)
    {
        if (_current == kind) Hide();
        else Show(kind);
    }
}
