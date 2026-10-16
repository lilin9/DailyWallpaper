using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using DailyWallpaper.Models;
using DailyWallpaper.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DailyWallpaper.Platforms.Android;

/// <summary>
/// Android 前台服务（需求 3.3.1）：保活、常驻通知，并在到点时执行壁纸切换。
/// </summary>
/// <remarks>
/// 常用策略组合：前台服务 + <c>START_STICKY</c> + <c>onTaskRemoved</c> 重启 + 开机广播 + 系统闹钟。
/// 但必须说明：在非 Root 设备上，系统仍可能在极端情况下终止应用，无法保证 100% 不被关闭。
/// </remarks>
[Service(
    Name = "com.dailywallpaper.app.WallpaperForegroundService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeSpecialUse)]
[Property("android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE", Value = "daily_wallpaper_auto_switch")]
public sealed class WallpaperForegroundService : Service
{
    /// <summary>启动服务（用户启用自动切换）。</summary>
    public const string ActionStart = "com.dailywallpaper.app.action.START";

    /// <summary>停止服务（用户暂停自动切换）。</summary>
    public const string ActionStop = "com.dailywallpaper.app.action.STOP";

    /// <summary>到点检查（来自闹钟或开机广播）。</summary>
    public const string ActionCheck = "com.dailywallpaper.app.action.CHECK";

    private ILogger? _logger;

    /// <summary>前台服务是否正在运行，供界面与调度器查询。</summary>
    public static bool IsRunning { get; private set; }

    /// <inheritdoc />
    public override void OnCreate()
    {
        base.OnCreate();

        var services = MauiProgram.Services;
        _logger = services?.GetService<ILoggerFactory>()?.CreateLogger<WallpaperForegroundService>();
        IsRunning = true;
    }

    /// <inheritdoc />
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var action = intent?.Action ?? ActionStart;

        try
        {
            // 前台服务必须先调用 StartForeground，否则系统会抛出 ANR / 崩溃。
            StartForeground(
                AndroidNotificationHelper.NotificationId,
                AndroidNotificationHelper.BuildOngoingNotification(this, DescribeNextSwitch()));
        }
        catch (Exception ex)
        {
            Log(WallpaperLogLevel.Warning, "显示前台服务通知失败（可能未授予通知权限）", ex);
        }

        if (action == ActionStop)
        {
            IsRunning = false;
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        // 启动时做一次过期补切 + 重新排程；到点检查时只执行到点任务。
        _ = Task.Run(async () =>
        {
            try
            {
                var services = MauiProgram.Services;
                var scheduler = services?.GetService<IWallpaperScheduler>();
                if (scheduler is null)
                {
                    return;
                }

                if (action == ActionStart)
                {
                    await scheduler.InitializeAsync().ConfigureAwait(false);
                }
                else
                {
                    await scheduler.RunIfDueAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log(WallpaperLogLevel.Error, "后台定时切换执行失败", ex);
            }
        });

        // START_STICKY：被系统回收后尽量重新创建并保持运行。
        return StartCommandResult.Sticky;
    }

    /// <summary>用户从最近任务中划掉应用时，尽量重新拉起服务（需求 3.3.1）。</summary>
    public override void OnTaskRemoved(Intent? rootIntent)
    {
        Log(WallpaperLogLevel.Info, "应用被从最近任务中移除，尝试重新启动前台服务");

        try
        {
            var restartIntent = new Intent(this, typeof(WallpaperForegroundService));
            restartIntent.SetAction(ActionCheck);

            var pendingIntent = PendingIntent.GetService(
                this,
                2002,
                restartIntent,
                PendingIntentFlags.OneShot | PendingIntentFlags.Immutable);

            var alarmManager = GetSystemService(AlarmService) as AlarmManager;
            if (alarmManager is not null && pendingIntent is not null)
            {
                // 延迟 1 秒重启，避免被系统的“刚被移除”状态直接拦截。
                alarmManager.SetExactAndAllowWhileIdle(
                    AlarmType.RtcWakeup,
                    Java.Lang.JavaSystem.CurrentTimeMillis() + 1000,
                    pendingIntent);
            }
        }
        catch (Exception ex)
        {
            Log(WallpaperLogLevel.Warning, "重启前台服务失败", ex);
        }

        base.OnTaskRemoved(rootIntent);
    }

    /// <inheritdoc />
    public override void OnDestroy()
    {
        IsRunning = false;
        base.OnDestroy();
    }

    /// <inheritdoc />
    public override IBinder? OnBind(Intent? intent) => null;

    private string DescribeNextSwitch()
    {
        var settings = MauiProgram.Services?.GetService<ISettingsService>()?.Current;
        if (settings is null)
        {
            return "正在等待下一次切换时间";
        }

        if (settings.Interval == SwitchInterval.Never)
        {
            return "当前为“永久”模式，不会自动切换；可在应用内手动切换";
        }

        return settings.NextSwitchTime is { } next
            ? $"下一次自动切换：{next:yyyy-MM-dd HH:mm}"
            : "正在等待下一次切换时间";
    }

    private void Log(WallpaperLogLevel level, string message, Exception? exception = null)
    {
        try
        {
            var log = MauiProgram.Services?.GetService<ILogService>();
            if (log is null)
            {
                _logger?.LogWarning("{Message}", message);
                return;
            }

            if (exception is null)
            {
                log.Write(level, message, source: "前台服务");
            }
            else
            {
                log.Write(level, message, exception, source: "前台服务");
            }
        }
        catch
        {
            // 日志失败不影响服务本身。
        }
    }
}
