using Android.App;
using Android.Content;
using Android.OS;

namespace PrintAgentAndroid.Services;

[BroadcastReceiver(Enabled = true, Exported = true, Label = "PrintAgent Android - Boot Receiver")]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public sealed class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent?.Action != Intent.ActionBootCompleted) return;

        var serviceIntent = new Intent(context, typeof(PrintAgentService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            context.StartForegroundService(serviceIntent);
        else
            context.StartService(serviceIntent);
    }
}
