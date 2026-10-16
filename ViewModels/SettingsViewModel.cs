using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyWallpaper.Models;
using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;

namespace DailyWallpaper.ViewModels;

/// <summary>
/// 设置页（需求 5.5）：开机自启、后台常驻说明、锁屏壁纸、缓存数量、日志、关于、手动退出。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IStartupService _startup;
    private readonly IWallpaperSetter _setter;
    private readonly IBackgroundScheduler _background;
    private readonly IWallpaperScheduler _scheduler;
    private readonly ICategoryService _categories;
    private readonly ILogService _log;
    private readonly IPermissionGuideService _permissions;
    private readonly IWallpaperCache _cache;

    private bool _suppressStartupToggle;
    private bool _suppressLockScreenToggle;
    private bool _suppressTrayToggle;
    private bool _suppressCacheSlider;

    public SettingsViewModel(
        ISettingsService settings,
        IStartupService startup,
        IWallpaperSetter setter,
        IBackgroundScheduler background,
        IWallpaperScheduler scheduler,
        ICategoryService categories,
        ILogService log,
        IPermissionGuideService permissions,
        IWallpaperCache cache)
    {
        _settings = settings;
        _startup = startup;
        _setter = setter;
        _background = background;
        _scheduler = scheduler;
        _categories = categories;
        _log = log;
        _permissions = permissions;
        _cache = cache;
    }

    /// <summary>是否显示权限引导入口（仅 Android 为 true）。</summary>
    public bool IsPermissionGuideVisible => _permissions.IsSupported;

    /// <summary>是否显示 Windows 专有选项。</summary>
    public bool IsWindows => DeviceInfo.Current.Platform == DevicePlatform.WinUI;

    public bool IsStartupSupported => _startup.IsSupported;

    public bool IsLockScreenSupported => _setter.SupportsLockScreen;

    public string StartupDescription => _startup.Description;

    public string BackgroundDescription => _background.Description;

    public string PlatformName => _setter.PlatformName;

    public string DeviceDescription { get; private set; } = "-";

    /// <summary>缓存张数（滑块用 double，保存时取整）。</summary>
    [ObservableProperty]
    public partial double CacheSize { get; set; } = Defaults.DefaultCacheSize;

    public string CacheSizeText => $"本地缓存 {CacheSizeValue} 张（用于去重与断网备选）";

    [ObservableProperty]
    public partial bool RunAtStartup { get; set; }

    [ObservableProperty]
    public partial bool MinimizeToTrayOnClose { get; set; }

    [ObservableProperty]
    public partial bool SetLockScreen { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    public string VersionText => $"{AppInfo.Current.Name} v{AppInfo.Current.VersionString} (build {AppInfo.Current.BuildString})";

    public string AppDataPath => FileSystem.AppDataDirectory;

    public string SettingsFilePath => _settings.SettingsFilePath;

    public string CategoriesFilePath => _categories.ConfigFilePath;

    public string LogFilePath => _log.LogFilePath;

    public string AboutText =>
        "DailyWallpaper 是一个完全离线的跨平台每日壁纸工具：不使用账号、不做云同步，"
        + "所有设置、图片与日志都只保存在本机。壁纸来自公开的免密钥图片接口（Ospyo / Uapi），"
        + "无需 API Key。\n\n"
        + "说明：非 Root 的 Android 设备无法保证应用 100% 不被系统杀死，本应用通过前台服务 + 开机自启 + "
        + "定时闹钟尽量提高存活能力；Windows 端默认常驻系统托盘。";

    /// <summary>页面显示时刷新。</summary>
    public Task LoadAsync()
    {
        var settings = _settings.Current;

        DeviceDescription = GetDeviceDescription();

        _suppressStartupToggle = true;
        RunAtStartup = _startup.IsEnabled;
        _suppressStartupToggle = false;

        _suppressTrayToggle = true;
        MinimizeToTrayOnClose = settings.MinimizeToTrayOnClose;
        _suppressTrayToggle = false;

        _suppressLockScreenToggle = true;
        SetLockScreen = settings.SetLockScreen;
        _suppressLockScreenToggle = false;

        _suppressCacheSlider = true;
        CacheSize = Math.Clamp(settings.CacheSize, 1, 20);
        _suppressCacheSlider = false;
        OnPropertyChanged(nameof(CacheSizeText));

        OnPropertyChanged(nameof(IsPermissionGuideVisible));
        OnPropertyChanged(nameof(IsWindows));
        OnPropertyChanged(nameof(IsStartupSupported));
        OnPropertyChanged(nameof(IsLockScreenSupported));

        return Task.CompletedTask;
    }

    /// <summary>打开权限引导页（Android）。</summary>
    [RelayCommand]
    private async Task OpenPermissionsAsync()
    {
        await Shell.Current.GoToAsync(AppShell.PermissionsRoute).ConfigureAwait(true);
    }

    /// <summary>把设置文件路径复制到剪贴板，便于用户手动编辑配置。</summary>
    [RelayCommand]
    private async Task CopyPathAsync(string? path)
    {
        var text = string.IsNullOrWhiteSpace(path) ? AppDataPath : path!;

        try
        {
            await Clipboard.Default.SetTextAsync(text).ConfigureAwait(true);
            IsError = false;
            Message = $"已复制路径：{text}";
        }
        catch (Exception ex)
        {
            IsError = true;
            Message = $"复制失败：{ex.Message}";
        }
    }

    /// <summary>清空日志。</summary>
    [RelayCommand]
    private async Task ClearLogsAsync()
    {
        await _log.ClearAsync().ConfigureAwait(true);
        IsError = false;
        Message = "日志已清空。";
    }

    /// <summary>手动退出应用（Windows 端只有通过此入口才会真正退出，Android 端会同时停止前台服务）。</summary>
    [RelayCommand]
    private async Task ExitAppAsync()
    {
#if WINDOWS
        Platforms.Windows.WindowsAppIntegration.ExitApplication();
        await Task.CompletedTask;
#elif ANDROID
        await _background.StopAsync().ConfigureAwait(true);
        _log.Write(WallpaperLogLevel.Info, "用户手动退出应用");

        try
        {
            global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid());
        }
        catch (Exception)
        {
            // 某些定制系统会阻止杀进程，忽略即可。
        }
#else
        await Task.CompletedTask;
#endif
    }

    partial void OnRunAtStartupChanged(bool value)
    {
        if (_suppressStartupToggle)
        {
            return;
        }

        _ = ApplyStartupAsync(value);
    }

    partial void OnMinimizeToTrayOnCloseChanged(bool value)
    {
        if (_suppressTrayToggle)
        {
            return;
        }

        _ = ApplyTrayPreferenceAsync(value);
    }

    partial void OnSetLockScreenChanged(bool value)
    {
        if (_suppressLockScreenToggle)
        {
            return;
        }

        _ = ApplyLockScreenPreferenceAsync(value);
    }

    partial void OnCacheSizeChanged(double value)
    {
        OnPropertyChanged(nameof(CacheSizeText));

        if (_suppressCacheSlider)
        {
            return;
        }

        _ = ApplyCacheSizeAsync(CacheSizeValue);
    }

    private int CacheSizeValue => (int)Math.Round(CacheSize);

    private async Task ApplyStartupAsync(bool enabled)
    {
        try
        {
            var applied = await _startup.SetEnabledAsync(enabled).ConfigureAwait(true);

            var settings = _settings.Current;
            settings.RunAtStartup = applied;
            await _settings.SaveAsync(settings).ConfigureAwait(true);

            if (applied != enabled)
            {
                _suppressStartupToggle = true;
                RunAtStartup = applied;
                _suppressStartupToggle = false;
            }

            IsError = false;
            Message = applied ? "已开启开机自启。" : "已关闭开机自启。";
        }
        catch (Exception ex)
        {
            IsError = true;
            Message = $"设置开机自启失败：{ex.Message}";
            _log.Write(WallpaperLogLevel.Error, "设置开机自启失败", ex);
        }
    }

    private async Task ApplyTrayPreferenceAsync(bool enabled)
    {
        var settings = _settings.Current;
        settings.MinimizeToTrayOnClose = enabled;
        await _settings.SaveAsync(settings).ConfigureAwait(true);

        IsError = false;
        Message = enabled ? "关闭主窗口时将最小化到系统托盘。" : "关闭主窗口时将直接退出应用。";
    }

    private async Task ApplyLockScreenPreferenceAsync(bool enabled)
    {
        var settings = _settings.Current;
        settings.SetLockScreen = enabled;
        await _settings.SaveAsync(settings).ConfigureAwait(true);

        IsError = false;
        Message = enabled
            ? "已开启：切换壁纸时同时设置锁屏壁纸（系统不支持时会自动降级）。"
            : "已关闭：只设置桌面壁纸。";
    }

    private async Task ApplyCacheSizeAsync(int size)
    {
        var settings = _settings.Current;
        settings.CacheSize = Math.Clamp(size, 1, 20);
        await _settings.SaveAsync(settings).ConfigureAwait(true);

        await _cache.CleanupAsync().ConfigureAwait(true);
    }

    /// <summary>打开应用数据目录（存放设置、缓存与日志）。</summary>
    [RelayCommand]
    private async Task OpenDataFolderAsync()
    {
#if WINDOWS
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppDataPath,
                UseShellExecute = true
            });
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            IsError = true;
            Message = $"打开目录失败：{ex.Message}";
        }
#else
        await CopyPathAsync(AppDataPath).ConfigureAwait(true);
#endif
    }

    private string GetDeviceDescription()
    {
        try
        {
            return $"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model} · "
                   + $"{DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}";
        }
        catch (Exception)
        {
            return "-";
        }
    }
}
