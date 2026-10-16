using Android.App;
using Android.Content;
using Android.OS;

namespace DailyWallpaper.Platforms.Android;

/// <summary>
/// 前台服务通知的构造工具（需求 3.3.1：前台服务必须显示持续通知）。
/// </summary>
internal static class AndroidNotificationHelper
{
    /// <summary>通知渠道 ID。</summary>
    public const string ChannelId = "dailywallpaper.ongoing";

    /// <summary>前台服务通知 ID。</summary>
    public const int NotificationId = 2001;

    /// <summary>创建通知渠道（Android 8.0+ 必须创建，否则通知不会显示）。</summary>
    public static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
        if (manager is null)
        {
            return;
        }

        if (manager.GetNotificationChannel(ChannelId) is not null)
        {
            return;
        }

        var channel = new NotificationChannel(ChannelId, "壁纸自动切换", NotificationImportance.Low)
        {
            Description = "用于保持每日壁纸后台服务运行，通知不可关闭但可折叠。"
        };

        channel.SetShowBadge(false);
        manager.CreateNotificationChannel(channel);
    }

    /// <summary>构建前台服务的持续通知（点击可回到应用主界面）。</summary>
    public static Notification BuildOngoingNotification(Context context, string text)
    {
        EnsureChannel(context);

        var launchIntent = context.PackageName is { Length: > 0 } packageName
            ? context.PackageManager?.GetLaunchIntentForPackage(packageName)
            : null;
        launchIntent?.AddFlags(ActivityFlags.SingleTop);

        PendingIntent? contentIntent = null;
        if (launchIntent is not null)
        {
            contentIntent = PendingIntent.GetActivity(
                context,
                0,
                launchIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        }

        Notification.Builder builder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new Notification.Builder(context, ChannelId)
            : new Notification.Builder(context);

        builder
            .SetContentTitle("每日壁纸正在后台运行")
            .SetContentText(text)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true);

        if (contentIntent is not null)
        {
            builder.SetContentIntent(contentIntent);
        }

        return builder.Build();
    }
}
