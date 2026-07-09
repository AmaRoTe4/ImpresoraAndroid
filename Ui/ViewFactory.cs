using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;

namespace PrintAgentAndroid.Ui;

public static class ViewFactory
{
    public static GradientDrawable RoundedBackground(int color, float radiusPx)
    {
        var gd = new GradientDrawable();
        gd.SetShape(ShapeType.Rectangle);
        gd.SetColor(new Color(color));
        gd.SetCornerRadius(radiusPx);
        return gd;
    }

    public static GradientDrawable PillBackground(int color)
    {
        var gd = new GradientDrawable();
        gd.SetShape(ShapeType.Rectangle);
        gd.SetColor(new Color(color));
        gd.SetCornerRadius(999f);
        return gd;
    }

    public static GradientDrawable CirclePillBackground(int color)
    {
        var gd = new GradientDrawable();
        gd.SetShape(ShapeType.Oval);
        gd.SetColor(new Color(color));
        return gd;
    }

    public static GradientDrawable BuildCircleBackground(Context context, bool active)
    {
        var gd = new GradientDrawable();
        gd.SetShape(ShapeType.Oval);
        gd.SetGradientType(GradientType.RadialGradient);
        gd.SetGradientRadius(Theme.DpToPx(context, 110));
        gd.SetGradientCenter(0.5f, 0.4f);
        gd.SetColors(active
            ? new[] { Theme.AccentMid, Theme.AccentDark }
            : new[] { Theme.InactiveGradFrom, Theme.InactiveGradTo });
        return gd;
    }

    public static TextView CreateStatusPill(Context context)
    {
        var tv = new TextView(context) { TextSize = 12 };
        tv.SetTypeface(tv.Typeface, TypefaceStyle.Bold);
        var padH = Theme.DpToPxInt(context, 12);
        var padV = Theme.DpToPxInt(context, 6);
        tv.SetPadding(padH, padV, padH, padV);
        return tv;
    }

    public static void ApplyStatusPill(TextView pill, bool active)
    {
        pill.Text = active ? "Activo" : "Inactivo";
        pill.SetTextColor(new Color(active ? Theme.Accent : Theme.Destructive));
        pill.Background = PillBackground(active ? Theme.AccentPillBg : Theme.DestructivePillBg);
    }

    public static ColorStateList SwitchTint(int onColor, int offColor)
    {
        var states = new int[][]
        {
            new[] { global::Android.Resource.Attribute.StateChecked },
            new[] { -global::Android.Resource.Attribute.StateChecked }
        };
        var colors = new[] { onColor, offColor };
        return new ColorStateList(states, colors);
    }

    public static Switch CreateTintedSwitch(Context context)
    {
        var sw = new Switch(context);
        sw.TrackTintList = SwitchTint(Theme.Accent, Theme.SwitchOffTrack);
        return sw;
    }

    public static (LinearLayout column, TextView valueView) CreateStatColumn(Context context, string label, string value, int valueColor)
    {
        var col = new LinearLayout(context) { Orientation = Orientation.Vertical };
        col.SetGravity(GravityFlags.CenterHorizontal);

        var lbl = new TextView(context) { Text = label, TextSize = 11 };
        lbl.SetTextColor(new Color(Theme.MutedForeground));
        col.AddView(lbl);

        var val = new TextView(context) { Text = value, TextSize = 15 };
        val.SetTypeface(Typeface.Monospace, TypefaceStyle.Bold);
        val.SetTextColor(new Color(valueColor));
        col.AddView(val);

        return (col, val);
    }
}
