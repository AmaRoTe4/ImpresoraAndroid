using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace PrintAgentAndroid.Ui;

public sealed class BottomNavBar
{
    public View Root { get; }
    public Action<PanelKind>? OnTabSelected;

    private readonly Dictionary<PanelKind, TextView> _labels = new();

    public BottomNavBar(Context context)
    {
        var bar = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        bar.Background = ViewFactory.RoundedBackground(AppTheme.Card, 0);
        var padV = AppTheme.DpToPxInt(context, 12);
        bar.SetPadding(0, padV, 0, padV);

        AddTab(context, bar, PanelKind.Config, "Config");
        AddTab(context, bar, PanelKind.Logs, "Logs");
        AddTab(context, bar, PanelKind.Test, "Test");

        Root = bar;
    }

    private void AddTab(Context context, LinearLayout bar, PanelKind kind, string label)
    {
        var tab = new LinearLayout(context) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true };
        tab.SetGravity(GravityFlags.CenterHorizontal);

        var text = new TextView(context) { Text = label, TextSize = 12 };
        text.SetTextColor(new Color(AppTheme.MutedForeground));
        tab.AddView(text);
        tab.Click += (_, _) => OnTabSelected?.Invoke(kind);

        _labels[kind] = text;
        bar.AddView(tab, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
    }

    public void SetActive(PanelKind? kind)
    {
        foreach (var (k, tv) in _labels)
            tv.SetTextColor(new Color(k == kind ? AppTheme.Accent : AppTheme.MutedForeground));
    }
}
