using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;
using RadioE45.Services.Audio;

namespace RadioE45;

[Service(
    Name = "com.radioe45.app.AndroidMediaNotificationService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
public sealed class AndroidMediaNotificationService : Service
{
    internal const string ActionRefresh = "com.radioe45.app.action.REFRESH_MEDIA_NOTIFICATION";
    internal const string ActionPause = "com.radioe45.app.action.PAUSE";
    internal const string ActionResume = "com.radioe45.app.action.RESUME";
    internal const string ActionStop = "com.radioe45.app.action.STOP";
    internal const string ActionDismiss = "com.radioe45.app.action.DISMISS";

    private const string ChannelId = "radioe45.playback";
    private const int NotificationId = 1045;

    private MediaSession? _mediaSession;
    private NotificationManager? _notificationManager;
    private bool _isForeground;
    private BecomingNoisyReceiver? _noisyReceiver;

    public override void OnCreate()
    {
        base.OnCreate();

        _notificationManager = GetSystemService(NotificationService) as NotificationManager;
        EnsureNotificationChannel();

        _mediaSession = new MediaSession(this, "RadioE45Playback");
        _mediaSession.SetCallback(new MediaSessionCallback(this));
        _mediaSession.SetSessionActivity(CreateContentPendingIntent());
        _mediaSession.Active = true;

        IAudioService? audio = IPlatformApplication.Current?.Services?.GetService<IAudioService>();
        if (audio is not null)
        {
            _noisyReceiver = new BecomingNoisyReceiver(audio);
            IntentFilter filter = new(AudioManager.ActionAudioBecomingNoisy);
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                RegisterReceiver(_noisyReceiver, filter, ReceiverFlags.NotExported);
            else
                RegisterReceiver(_noisyReceiver, filter);
        }
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        string? action = intent?.Action;

        switch (action)
        {
            case ActionPause:
                _ = ExecuteAudioActionAsync(audio => audio.PauseAsync());
                break;
            case ActionResume:
                _ = ExecuteAudioActionAsync(audio => audio.ResumeAsync());
                break;
            case ActionStop:
                _ = ExecuteAudioActionAsync(audio => audio.StopAsync());
                return StartCommandResult.NotSticky;
            case ActionDismiss:
                StopServiceNotification();
                return StartCommandResult.NotSticky;
        }

        PublishNotification();
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        if (_noisyReceiver is not null)
        {
            UnregisterReceiver(_noisyReceiver);
            _noisyReceiver = null;
        }

        StopServiceNotification();
        _mediaSession?.Release();
        _mediaSession = null;
        base.OnDestroy();
    }

    internal static void RequestRefresh(Context context) => StartService(context, ActionRefresh, preferForeground: true);

    internal static void RequestStop(Context context) => StartService(context, ActionDismiss, preferForeground: false);

    private static void StartService(Context context, string action, bool preferForeground)
    {
        Intent intent = new(context, typeof(AndroidMediaNotificationService));
        intent.SetAction(action);

        try
        {
            if (preferForeground && OperatingSystem.IsAndroidVersionAtLeast(26))
                context.StartForegroundService(intent);
            else
                context.StartService(intent);
        }
        catch (Exception ex) when (
            ex is Java.Lang.IllegalStateException ||
            ex is Android.App.ForegroundServiceStartNotAllowedException)
        {
            // ForegroundServiceStartNotAllowedException (API 31+): the system blocks FGS
            // starts from the background when the app has no active MediaSession exemption.
            // Control should flow through the existing active MediaSession instead.
            System.Diagnostics.Debug.WriteLine($"[RadioE45] FGS start blocked: {ex.Message}");
        }
    }

    private void PublishNotification()
    {
        if (_mediaSession is null || _notificationManager is null)
            return;

        (PlatformNowPlayingSnapshot snapshot, Android.Graphics.Bitmap? artwork) = AndroidNowPlayingStateStore.GetState();
        if (string.IsNullOrWhiteSpace(snapshot.Title) && string.IsNullOrWhiteSpace(snapshot.Artist))
        {
            StopServiceNotification();
            return;
        }

        _mediaSession.Active = true;
        _mediaSession.SetMetadata(BuildMetadata(snapshot, artwork));
        _mediaSession.SetPlaybackState(BuildPlaybackState(snapshot));

        Notification notification = BuildNotification(snapshot, artwork);
        if (!_isForeground)
        {
            StartForegroundInternal(notification);
            _isForeground = true;
        }
        else
        {
            _notificationManager.Notify(NotificationId, notification);
        }
    }

    private Notification BuildNotification(PlatformNowPlayingSnapshot snapshot, Android.Graphics.Bitmap? artwork)
    {
        Notification.Action transportAction = snapshot.IsPlaying
            ? CreateAction(ActionPause, Android.Resource.Drawable.IcMediaPause, "Pausa")
            : CreateAction(ActionResume, Android.Resource.Drawable.IcMediaPlay, "Riprendi");

        Notification.Action stopAction = CreateAction(ActionStop, Android.Resource.Drawable.IcMenuCloseClearCancel, "Stop");

        Notification.Builder builder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new Notification.Builder(this, ChannelId)
            : new Notification.Builder(this);

        builder
            .SetContentTitle(snapshot.Title)
            .SetContentText(string.IsNullOrWhiteSpace(snapshot.Artist) ? "RadioE45" : snapshot.Artist)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentIntent(CreateContentPendingIntent())
            .SetDeleteIntent(CreateServicePendingIntent(ActionStop, 4))
            .SetVisibility(NotificationVisibility.Public)
            .SetOnlyAlertOnce(true)
            .SetShowWhen(false)
            .SetOngoing(snapshot.IsPlaying)
            .SetStyle(new Notification.MediaStyle()
                .SetMediaSession(_mediaSession!.SessionToken!)!
                .SetShowActionsInCompactView(0, 1))
            .AddAction(transportAction)
            .AddAction(stopAction);

        if (artwork is not null)
            builder.SetLargeIcon(artwork);

        return builder.Build()!;
    }

    private Notification.Action CreateAction(string action, int iconResId, string title)
    {
        PendingIntent pendingIntent = CreateServicePendingIntent(action, title.GetHashCode(StringComparison.Ordinal));
        Notification.Action.Builder actionBuilder = OperatingSystem.IsAndroidVersionAtLeast(23)
            ? new Notification.Action.Builder(Android.Graphics.Drawables.Icon.CreateWithResource(this, iconResId), title, pendingIntent)
            : new Notification.Action.Builder(iconResId, title, pendingIntent);
        return actionBuilder.Build()!;
    }

    private PendingIntent CreateContentPendingIntent()
    {
        Intent intent = new(this, typeof(MainActivity));
        intent.SetAction(Intent.ActionMain);
        intent.AddCategory(Intent.CategoryLauncher);
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        return PendingIntent.GetActivity(
            this,
            1,
            intent,
            CreatePendingIntentFlags())!;
    }

    private PendingIntent CreateServicePendingIntent(string action, int requestCode)
    {
        Intent intent = new(this, typeof(AndroidMediaNotificationService));
        intent.SetAction(action);

        return PendingIntent.GetService(
            this,
            requestCode,
            intent,
            CreatePendingIntentFlags())!;
    }

    private MediaMetadata BuildMetadata(PlatformNowPlayingSnapshot snapshot, Android.Graphics.Bitmap? artwork)
    {
        MediaMetadata.Builder builder = new MediaMetadata.Builder();
        builder.PutString(MediaMetadata.MetadataKeyTitle, snapshot.Title);
        builder.PutString(MediaMetadata.MetadataKeyArtist, snapshot.Artist);
        builder.PutString(MediaMetadata.MetadataKeyDisplayTitle, snapshot.Title);
        builder.PutString(MediaMetadata.MetadataKeyDisplaySubtitle, snapshot.Artist);

        if (snapshot.DurationSeconds.HasValue)
            builder.PutLong(MediaMetadata.MetadataKeyDuration, snapshot.DurationSeconds.Value * 1000L);

        if (artwork is not null)
        {
            builder.PutBitmap(MediaMetadata.MetadataKeyAlbumArt, artwork);
            builder.PutBitmap(MediaMetadata.MetadataKeyDisplayIcon, artwork);
        }

        return builder.Build()!;
    }

    private PlaybackState BuildPlaybackState(PlatformNowPlayingSnapshot snapshot)
    {
        const long actions = PlaybackState.ActionPlay |
                             PlaybackState.ActionPause |
                             PlaybackState.ActionPlayPause |
                             PlaybackState.ActionStop;

        PlaybackStateCode state = snapshot.IsPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused;
        long position = snapshot.ElapsedSeconds.HasValue
            ? snapshot.ElapsedSeconds.Value * 1000L
            : PlaybackState.PlaybackPositionUnknown;
        float speed = snapshot.IsPlaying ? 1f : 0f;

        PlaybackState.Builder psBuilder = new PlaybackState.Builder();
        psBuilder.SetActions(actions);
        psBuilder.SetState(state, position, speed, SystemClock.ElapsedRealtime());
        return psBuilder.Build()!;
    }

    private static PendingIntentFlags CreatePendingIntentFlags()
    {
        PendingIntentFlags flags = PendingIntentFlags.UpdateCurrent;

        if (OperatingSystem.IsAndroidVersionAtLeast(23))
            flags |= PendingIntentFlags.Immutable;

        return flags;
    }

    private void StartForegroundInternal(Notification notification)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, notification, ForegroundService.TypeMediaPlayback);
        else
            StartForeground(NotificationId, notification);
    }

    private void StopServiceNotification()
    {
        if (_notificationManager is not null)
            _notificationManager.Cancel(NotificationId);

        if (_isForeground)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(24))
                StopForeground(StopForegroundFlags.Remove);
            else
                StopForeground(true);
        }

        _isForeground = false;
        StopSelf();
    }

    private void EnsureNotificationChannel()
    {
        if (_notificationManager is null || !OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        NotificationChannel channel = new(
            ChannelId,
            "Riproduzione RadioE45",
            NotificationImportance.Low)
        {
            Description = "Controlli di riproduzione e now playing di RadioE45"
        };

        channel.LockscreenVisibility = NotificationVisibility.Public;
        _notificationManager.CreateNotificationChannel(channel);
    }

    private async Task ExecuteAudioActionAsync(Func<IAudioService, Task> action)
    {
        IAudioService? audio = IPlatformApplication.Current?.Services?.GetService<IAudioService>();
        if (audio is null)
            return;

        await action(audio);
    }

    private sealed class MediaSessionCallback : MediaSession.Callback
    {
        private readonly AndroidMediaNotificationService _service;

        public MediaSessionCallback(AndroidMediaNotificationService service)
        {
            _service = service;
        }

        public override void OnPause()
        {
            _ = _service.ExecuteAudioActionAsync(audio => audio.PauseAsync());
        }

        public override void OnPlay()
        {
            _ = _service.ExecuteAudioActionAsync(audio => audio.ResumeAsync());
        }

        public override void OnStop()
        {
            _ = _service.ExecuteAudioActionAsync(audio => audio.StopAsync());
        }
    }
}
