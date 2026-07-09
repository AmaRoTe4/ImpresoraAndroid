using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using PrintAgentAndroid.Http;
using PrintAgentAndroid.Printing;

namespace PrintAgentAndroid.Services;

[Service(Exported = false, Label = "PrintAgent Android - Servicio", ForegroundServiceType = ForegroundService.TypeConnectedDevice)]
public sealed class PrintAgentService : Service
{
    private const string ChannelId = "printagent_channel";
    private const int NotificationId = 1001;

    public UsbEscPosPrinter? Printer { get; private set; }
    public LocalHttpServer? Server { get; private set; }
    public event Action<string>? LogEmitted;

    public sealed class LocalBinder : Binder
    {
        public PrintAgentService Service { get; }
        public LocalBinder(PrintAgentService service) => Service = service;
    }

    public override void OnCreate()
    {
        base.OnCreate();
        EnsureRunning();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        EnsureRunning();
        return StartCommandResult.Sticky;
    }

    public override IBinder OnBind(Intent? intent)
    {
        EnsureRunning();
        return new LocalBinder(this);
    }

    private void EnsureRunning()
    {
        if (Server != null) return;

        try
        {
            Printer = new UsbEscPosPrinter(this, msg => LogEmitted?.Invoke(msg));
            Server = new LocalHttpServer(Printer, msg => LogEmitted?.Invoke(msg), port: 5000);
            _ = Server.StartAsync();

            var notification = BuildNotification();
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                StartForeground(NotificationId, notification, ForegroundService.TypeConnectedDevice);
            else
                StartForeground(NotificationId, notification);
        }
        catch (Exception ex)
        {
            LogEmitted?.Invoke("Error arrancando el servicio: " + ex.Message);
            throw;
        }
    }

    private Notification BuildNotification()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var nm = (NotificationManager)GetSystemService(NotificationService)!;
            if (nm.GetNotificationChannel(ChannelId) == null)
            {
                var channel = new NotificationChannel(ChannelId, "PrintAgent", NotificationImportance.Low)
                {
                    Description = "Servidor de impresión activo en segundo plano"
                };
                nm.CreateNotificationChannel(channel);
            }
        }

#pragma warning disable CA1416
        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, ChannelId)
            : new Notification.Builder(this);
#pragma warning restore CA1416

        builder.SetContentTitle("PrintAgent Android")
            .SetContentText("Servidor de impresión activo en http://0.0.0.0:5000")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuInfoDetails)
            .SetOngoing(true);

        return builder.Build()!;
    }

    public override void OnDestroy()
    {
        Server?.Stop();
        Printer?.Dispose();
        base.OnDestroy();
    }

    /// <summary>
    /// El atributo [Service] de .NET Android no expone stopWithTask="false", así que
    /// por defecto Android mata este proceso cuando sacan la app de "Recientes" con un
    /// swipe. Workaround estándar: programar un reinicio casi inmediato vía AlarmManager.
    /// </summary>
    public override void OnTaskRemoved(Intent? rootIntent)
    {
        var restartIntent = new Intent(ApplicationContext, typeof(PrintAgentService)).SetPackage(PackageName);
        var restartPendingIntent = PendingIntent.GetService(
            this, 1, restartIntent, PendingIntentFlags.OneShot | PendingIntentFlags.Immutable);

        var alarmManager = (AlarmManager?)GetSystemService(AlarmService);
        alarmManager?.SetAndAllowWhileIdle(AlarmType.ElapsedRealtime, SystemClock.ElapsedRealtime() + 1000, restartPendingIntent);

        base.OnTaskRemoved(rootIntent);
    }
}
