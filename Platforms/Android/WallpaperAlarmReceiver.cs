using Android.App;
using Android.Content;
using DailyWallpaper.Models;
using DailyWallpaper.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DailyWallpaper.Platforms.Android;

/// <summary>
/// 系统闹钟触发时的接收器（需求 3.3.1）：到点后执行一次自动切换检查。
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class WallpaperAlarmReceiver : BroadcastReceiver
{
    /// <summary>闹钟 Intent 的 Action。</summary>
    public const string ActionAlarmTriggered = "com.dailywallpaper.app.action.ALARM";

    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
        {
            return;
        }

        var services = MauiProgram.Services;
        if (services is null)
        {
            return;
        }

        // 尽量让前台服务处于运行状态（从后台启动前台服务在 Android 12+ 可能被拒绝，失败不影响下面的切换）。
        AndroidBackgroundScheduler.StartForegroundService(WallpaperForegroundService.ActionCheck, "闹钟到点");

        var log = services.GetService<ILogService>();
        log?.Write(WallpaperLogLevel.Info, "收到系统闹钟，开始检查是否需要切换壁纸", source: "闹钟");

        // 使用 GoAsync 在接收器返回后继续完成下载与设置，避免主线程阻塞导致 ANR。
        var pendingResult = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                var scheduler = services.GetService<IWallpaperScheduler>();
                if (scheduler is not null)
                {
                    await scheduler.RunIfDueAsync().ConfigureAwait(false);
                    await scheduler.RescheduleAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                log?.Write(WallpaperLogLevel.Error, "闹钟触发的切换失败", ex, source: "闹钟");
            }
            finally
            {
                pendingResult?.Finish();
            }
        });
    }
}
