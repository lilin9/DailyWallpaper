using Android.Content;
using DailyWallpaper.Models;
using DailyWallpaper.Services;

namespace DailyWallpaper.Platforms.Android;

/// <summary>
/// 跳转到 Android 系统设置页面的辅助方法（权限引导页与自启动设置共用）。
/// </summary>
internal static class AndroidSettingsLauncher
{
    /// <summary>打开应用详情页。</summary>
    public static void OpenAppDetails()
    {
        StartActivity(
            new Intent(
                global::Android.Provider.Settings.ActionApplicationDetailsSettings,
                global::Android.Net.Uri.Parse("package:" + PackageName)),
            "打开应用详情页");
    }

    /// <summary>打开“自启动管理”：优先小米 HyperOS / MIUI，其次通用应用详情页。</summary>
    public static void OpenAutoStart()
    {
        // MIUI / HyperOS 的自启动管理页面。
        var miuiComponent = new ComponentName(
            "com.miui.securitycenter",
            "com.miui.permcenter.autostart.AutoStartManagementActivity");

        var intent = new Intent();
        intent.SetComponent(miuiComponent);

        if (TryStartActivity(intent))
        {
            return;
        }

        OpenAppDetails();
    }

    /// <summary>打开“闹钟与提醒”（精确闹钟）权限页面（Android 12+）。</summary>
    public static void OpenExactAlarmSettings()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            OpenAppDetails();
            return;
        }

        var intent = new Intent(
            global::Android.Provider.Settings.ActionRequestScheduleExactAlarm,
            global::Android.Net.Uri.Parse("package:" + PackageName));

        if (!TryStartActivity(intent))
        {
            OpenAppDetails();
        }
    }

    /// <summary>打开“电池优化”请求页面（Android 6+）。</summary>
    public static void OpenBatteryOptimizationSettings()
    {
        var intent = new Intent(
            global::Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations,
            global::Android.Net.Uri.Parse("package:" + PackageName));

        if (!TryStartActivity(intent))
        {
            OpenAppDetails();
        }
    }

    /// <summary>打开省电策略 / 电池使用详情页。</summary>
    public static void OpenBatterySaverSettings()
    {
        var intent = new Intent(global::Android.Provider.Settings.ActionBatterySaverSettings);

        if (!TryStartActivity(intent))
        {
            OpenAppDetails();
        }
    }

    private static string PackageName => global::Android.App.Application.Context.PackageName ?? string.Empty;

    private static void StartActivity(Intent intent, string actionName)
    {
        if (!TryStartActivity(intent))
        {
            var log = MauiProgram.Services?.GetService(typeof(ILogService)) as ILogService;
            log?.Write(WallpaperLogLevel.Warning, $"无法跳转到系统页面：{actionName}，请在系统设置中手动操作");
        }
    }

    private static bool TryStartActivity(Intent intent)
    {
        try
        {
            var context = global::Android.App.Application.Context;
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
            return true;
        }
        catch (Exception)
        {
            // 部分机型的定制系统没有对应页面，返回失败以便使用兜底页面。
            return false;
        }
    }
}
