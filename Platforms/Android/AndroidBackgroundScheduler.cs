using Android.App;
using Android.Content;
using Android.OS;
using DailyWallpaper.Models;
using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;

namespace DailyWallpaper.Platforms.Android;

/// <summary>
/// Android 后台常驻与定时（需求 3.3.1）：
/// 前台服务负责常驻与通知，<c>AlarmManager</c> 负责在系统允许的情况下精确唤醒。
/// </summary>
public sealed class AndroidBackgroundScheduler : IBackgroundScheduler
{
    /// <summary>AlarmManager 的请求码，固定值便于取消 / 覆盖同一个闹钟。</summary>
    internal const int AlarmRequestCode = 1001;

    private readonly ISettingsService _settings;
    private readonly ILogService _log;

    public AndroidBackgroundScheduler(ISettingsService settings, ILogService log)
    {
        _settings = settings;
        _log = log;
    }

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public string Description =>
        "通过前台服务 + 前台通知常驻内存，并使用系统闹钟在到点时唤醒。受系统限制，无法保证 100% 不被系统终止。";

    /// <inheritdoc />
    public Task<bool> IsRunningAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(WallpaperForegroundService.IsRunning || _settings.Current.IsEnabled);

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;
        if (!settings.IsEnabled)
        {
            settings.IsEnabled = true;
            await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        StartForegroundService(WallpaperForegroundService.ActionStart, "用户已启用后台自动切换");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;
        if (settings.IsEnabled)
        {
            settings.IsEnabled = false;
            await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        CancelAlarm();
        StartForegroundService(WallpaperForegroundService.ActionStop, "用户已暂停后台自动切换");
    }

    /// <inheritdoc />
    public Task ScheduleNextAsync(DateTime? nextTime, SwitchInterval interval, CancellationToken cancellationToken = default)
    {
        var context = global::Android.App.Application.Context;

        try
        {
            if (nextTime is null || interval == SwitchInterval.Never)
            {
                CancelAlarm();
                return Task.CompletedTask;
            }

            var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;
            if (alarmManager is null)
            {
                return Task.CompletedTask;
            }

            var intent = new Intent(context, typeof(WallpaperAlarmReceiver));
            intent.SetAction(WallpaperAlarmReceiver.ActionAlarmTriggered);

            var pendingIntent = PendingIntent.GetBroadcast(
                context,
                AlarmRequestCode,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            if (pendingIntent is null)
            {
                return Task.CompletedTask;
            }

            var triggerAt = nextTime.Value;
            var triggerMillis = new DateTimeOffset(triggerAt.ToUniversalTime()).ToUnixTimeMilliseconds();

            // Android 12+ 需要用户授予“闹钟与提醒”权限才能使用精确闹钟（需求 3.3.1）。
            if (OperatingSystem.IsAndroidVersionAtLeast(31) && !alarmManager.CanScheduleExactAlarms())
            {
                alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
                _log.Write(
                    WallpaperLogLevel.Warning,
                    "未获得精确闹钟权限，已使用非精确闹钟（切换时间可能被系统推迟），可在权限引导页开启");
            }
            else
            {
                alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
            }

            _log.Write(WallpaperLogLevel.Info, $"已设置下一次自动切换闹钟：{triggerAt:yyyy-MM-dd HH:mm}");
        }
        catch (Exception ex)
        {
            _log.Write(WallpaperLogLevel.Warning, "设置系统闹钟失败，将依赖前台服务内部定时器", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>取消已设置的闹钟。</summary>
    internal void CancelAlarm()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;
            var intent = new Intent(context, typeof(WallpaperAlarmReceiver));
            intent.SetAction(WallpaperAlarmReceiver.ActionAlarmTriggered);

            var pendingIntent = PendingIntent.GetBroadcast(
                context,
                AlarmRequestCode,
                intent,
                PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);

            if (pendingIntent is not null)
            {
                alarmManager?.Cancel(pendingIntent);
                pendingIntent.Cancel();
            }
        }
        catch (Exception ex)
        {
            _log.Write(WallpaperLogLevel.Warning, "取消系统闹钟失败", ex);
        }
    }

    /// <summary>
    /// 启动前台服务；Android 12+ 从后台启动前台服务受限，失败只记录日志（不影响已有的前台服务）。
    /// </summary>
    internal static void StartForegroundService(string action, string reason)
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(WallpaperForegroundService));
            intent.SetAction(action);

            // StartForegroundService 需要 API 26+；本项目最低支持 API 24，低版本直接使用 StartService。
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
        }
        catch (Exception ex)
        {
            var log = MauiProgram.Services?.GetService(typeof(ILogService)) as ILogService;
            log?.Write(WallpaperLogLevel.Warning, $"启动前台服务失败（{reason}）", ex);
        }
    }
}
