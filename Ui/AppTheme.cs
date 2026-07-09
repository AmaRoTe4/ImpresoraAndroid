using Android.Content;
using Android.Util;

namespace PrintAgentAndroid.Ui;

public static class AppTheme
{
    public const int Background = unchecked((int)0xFF0D0F14);
    public const int Card = unchecked((int)0xFF161920);
    public const int Muted = unchecked((int)0xFF1A1D24);
    public const int Border = unchecked((int)0x14FFFFFF);
    public const int Foreground = unchecked((int)0xFFE8EAF0);
    public const int MutedForeground = unchecked((int)0xFF6B7280);

    public const int Accent = unchecked((int)0xFF22C55E);
    public const int AccentDark = unchecked((int)0xFF15803D);
    public const int AccentMid = unchecked((int)0xFF16A34A);
    public const int AccentForeground = unchecked((int)0xFF0A1A0F);
    public const int Destructive = unchecked((int)0xFFEF4444);
    public const int SwitchOffTrack = unchecked((int)0xFF374151);
    public const int InactiveGradFrom = unchecked((int)0xFF374151);
    public const int InactiveGradTo = unchecked((int)0xFF1F2937);

    public const int AccentPillBg = unchecked((int)0x2622C55E);
    public const int DestructivePillBg = unchecked((int)0x26EF4444);

    public static float DpToPx(Context context, float dp) =>
        TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, context.Resources!.DisplayMetrics);

    public static int DpToPxInt(Context context, float dp) => (int)DpToPx(context, dp);
}
