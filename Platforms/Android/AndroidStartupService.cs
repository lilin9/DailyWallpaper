using Android.Content;
using DailyWallpaper.Models;
using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;

namespace DailyWallpaper.Platforms.Android;

/// <summary>
/// Android 开机自启（需求 3.3.1）。
/// </summary>
/// <remarks>
/// Android / HyperOS 不允许应用自行开启“自启动”权限，只能由用户在系统设置中手动允许，
/// 因此这里只负责引导跳转，并把用户的选择记录在本地设置中。
/// </remarks>
public sealed class AndroidStartupService : IStartupService
{
    private readonly ISettingsService _settings;
    private readonly ILogService _log;

    public AndroidStartupService(ISettingsService settings, ILogService log)
    {
        _settings = settings;
        _log = log;
    }

    /// <inheritdoc />
    public bool IsSupported => false;

    /// <inheritdoc />
    public bool IsEnabled => _settings.Current.RunAtStartup;

    /// <inheritdoc />
    public string Description =>
        "Android 无法由应用自动开启自启动，需要在系统“自启动管理 / 省电策略”中允许本应用，开机后才会自动启动并切换壁纸。";

    /// <inheritdoc />
    public async Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;
        settings.RunAtStartup = enabled;
        await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);

        if (enabled)
        {
            _log.Write(WallpaperLogLevel.Info, "已记录开启开机自启，请到系统自启动管理中允许本应用");
            AndroidSettingsLauncher.OpenAutoStart();
        }
        else
        {
            _log.Write(WallpaperLogLevel.Info, "已关闭开机自启（BOOT_COMPLETED 收到后不再启动服务）");
        }

        return settings.RunAtStartup;
    }
}
