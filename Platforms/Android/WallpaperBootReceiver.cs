using Android.App;
using Android.Content;
using DailyWallpaper.Models;
using DailyWallpaper.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DailyWallpaper.Platforms.Android;

/// <summary>
/// 开机广播接收器（需求 3.3.1）：开机后启动前台服务并重新安排下一次切换。
/// </summary>
/// <remarks>
/// 需要 <c>RECEIVE_BOOT_COMPLETED</c> 权限，并且用户需要在系统自启动管理中允许本应用，
/// 否则部分定制系统（如 MIUI / HyperOS）不会派发该广播。
/// </remarks>
[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = true)]
[IntentFilter(new[]
{
    Intent.ActionBootCompleted,
    Intent.ActionMyPackageReplaced,
    "android.intent.action.QUICKBOOT_POWERON"
})]
public sealed class WallpaperBootReceiver : BroadcastReceiver
{
    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        var services = MauiProgram.Services;
        if (services is null)
        {
            return;
        }

        var settings = services.GetService<ISettingsService>()?.Current;
        var log = services.GetService<ILogService>();

        if (settings is not null && !settings.RunAtStartup)
        {
            log?.Write(WallpaperLogLevel.Info, "已关闭开机自启，忽略开机广播", source: "开机广播");
            return;
        }

        log?.Write(
            WallpaperLogLevel.Info,
            $"收到开机广播（{intent?.Action}），正在启动后台服务",
            source: "开机广播");

        AndroidBackgroundScheduler.StartForegroundService(WallpaperForegroundService.ActionStart, "开机自启");
    }
}
