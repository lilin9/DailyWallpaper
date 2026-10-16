using DailyWallpaper.Models;
using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;

namespace DailyWallpaper.Platforms.Windows;

/// <summary>
/// Windows 后台常驻：应用常驻系统托盘，定时切换由共享的 <see cref="IWallpaperScheduler"/> 定时器驱动，
/// 因此这里只负责“启用 / 暂停自动切换”的状态管理。
/// </summary>
public sealed class WindowsBackgroundScheduler : IBackgroundScheduler
{
    private readonly ISettingsService _settings;
    private readonly ILogService _log;

    public WindowsBackgroundScheduler(ISettingsService settings, ILogService log)
    {
        _settings = settings;
        _log = log;
    }

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public string Description =>
        "Windows 端应用常驻系统托盘，关闭主窗口不会退出；定时切换由应用内定时器执行。";

    /// <inheritdoc />
    public Task<bool> IsRunningAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_settings.Current.IsEnabled);

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;
        if (!settings.IsEnabled)
        {
            settings.IsEnabled = true;
            await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            _log.Write(WallpaperLogLevel.Info, "已启用自动切换（Windows 常驻托盘）");
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;
        if (settings.IsEnabled)
        {
            settings.IsEnabled = false;
            await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            _log.Write(WallpaperLogLevel.Info, "已暂停自动切换（应用仍常驻托盘）");
        }
    }

    /// <inheritdoc />
    public Task ScheduleNextAsync(DateTime? nextTime, SwitchInterval interval, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
