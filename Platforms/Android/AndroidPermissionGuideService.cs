using Android.Content;
using Android.Content.PM;
using Android.OS;
using DailyWallpaper.Models;
using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;

namespace DailyWallpaper.Platforms.Android;

/// <summary>
/// Android 权限引导（需求 5.4）：引导用户开启通知、精确闹钟、关闭电池优化、开启自启动、锁定后台。
/// </summary>
/// <remarks>
/// 受系统限制，应用无法自行完成这些设置，也无法读取部分开关的真实状态，因此这里以“可达成的引导”为目标。
/// </remarks>
public sealed class AndroidPermissionGuideService : IPermissionGuideService
{
    /// <summary>通知权限（Android 13+）。</summary>
    public const string Notifications = "notifications";

    /// <summary>精确闹钟权限（Android 12+）。</summary>
    public const string ExactAlarm = "exact_alarm";

    /// <summary>电池优化白名单。</summary>
    public const string BatteryOptimization = "battery_optimization";

    /// <summary>自启动管理。</summary>
    public const string AutoStart = "auto_start";

    /// <summary>省电策略“无限制”。</summary>
    public const string BatterySaver = "battery_saver";

    /// <summary>最近任务中锁定应用。</summary>
    public const string TaskLock = "task_lock";

    private readonly ILogService _log;

    public AndroidPermissionGuideService(ILogService log)
    {
        _log = log;
    }

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public Task<IReadOnlyList<PermissionGuideItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PermissionGuideItem> items = new List<PermissionGuideItem>
        {
            new(
                Notifications,
                "允许发送通知",
                "Android 13 起需要授权通知，否则前台服务的常驻通知无法显示（服务仍会运行）。",
                "去授权",
                HasNotificationPermission()),
            new(
                ExactAlarm,
                "允许设置精确闹钟",
                "Android 12 起精确闹钟需要单独授权；未授权时切换时间可能被系统推迟几分钟到几小时。",
                "去授权",
                CanScheduleExactAlarms()),
            new(
                BatteryOptimization,
                "关闭电池优化（加入白名单）",
                "允许应用在熄屏后继续运行，避免被系统冻结导致无法按时切换壁纸。",
                "去设置",
                IsIgnoringBatteryOptimizations()),
            new(
                BatterySaver,
                "省电策略设为“无限制”",
                "小米 HyperOS：设置 → 应用设置 → 应用管理 → 本应用 → 省电策略 → 选择“无限制”。",
                "打开省电设置",
                false),
            new(
                AutoStart,
                "开启自启动",
                "小米 HyperOS：设置 → 应用设置 → 应用管理 → 本应用 → 自启动 中开启；否则开机后不会自动启动。",
                "去开启",
                false),
            new(
                TaskLock,
                "在最近任务中锁定应用",
                "打开最近任务列表，长按本应用的卡片选择“锁定”，可避免一键清理后台时被结束。",
                "打开应用详情",
                false)
        };

        return Task.FromResult(items);
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(string itemId, CancellationToken cancellationToken = default)
    {
        try
        {
            switch (itemId)
            {
                case Notifications:
                    var status = await Permissions.RequestAsync<Permissions.PostNotifications>().ConfigureAwait(false);
                    _log.Write(
                        WallpaperLogLevel.Info,
                        status == PermissionStatus.Granted ? "已获得通知权限" : "通知权限未授予");
                    return status == PermissionStatus.Granted;

                case ExactAlarm:
                    AndroidSettingsLauncher.OpenExactAlarmSettings();
                    return CanScheduleExactAlarms();

                case BatteryOptimization:
                    AndroidSettingsLauncher.OpenBatteryOptimizationSettings();
                    return IsIgnoringBatteryOptimizations();

                case BatterySaver:
                    AndroidSettingsLauncher.OpenBatterySaverSettings();
                    return false;

                case AutoStart:
                    AndroidSettingsLauncher.OpenAutoStart();
                    return false;

                case TaskLock:
                    AndroidSettingsLauncher.OpenAppDetails();
                    return false;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _log.Write(WallpaperLogLevel.Warning, $"权限引导操作失败：{itemId}", ex);
            return false;
        }
    }

    /// <summary>是否已获得通知权限（Android 13 以下视为已授权）。</summary>
    public static bool HasNotificationPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return true;
        }

        try
        {
            var context = global::Android.App.Application.Context;
            return context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) == Permission.Granted;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>是否可以设置精确闹钟（Android 12 以下视为可以）。</summary>
    public static bool CanScheduleExactAlarms()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            return true;
        }

        try
        {
            var context = global::Android.App.Application.Context;
            var alarmManager = context.GetSystemService(Context.AlarmService) as global::Android.App.AlarmManager;
            return alarmManager?.CanScheduleExactAlarms() ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>是否已加入电池优化白名单（Android 6 以下视为已加入）。</summary>
    public static bool IsIgnoringBatteryOptimizations()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            return true;
        }

        try
        {
            var context = global::Android.App.Application.Context;
            var powerManager = context.GetSystemService(Context.PowerService) as PowerManager;
            return powerManager?.IsIgnoringBatteryOptimizations(context.PackageName ?? string.Empty) ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
