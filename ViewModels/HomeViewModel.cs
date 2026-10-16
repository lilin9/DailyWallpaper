using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyWallpaper.Models;
using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;

namespace DailyWallpaper.ViewModels;

/// <summary>
/// 主页（需求 5.1）：当前壁纸预览、下次切换时间、启用 / 暂停自动切换、立即切换。
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IWallpaperRotationService _rotation;
    private readonly IWallpaperScheduler _scheduler;
    private readonly ILogService _log;
    private readonly IBackgroundScheduler _background;
    private readonly IDeviceDisplayService _display;
    private readonly IWallpaperSetter _setter;

    /// <summary>刷新界面时抑制开关的联动逻辑，避免与用户操作互相触发。</summary>
    private bool _suppressToggle;

    public HomeViewModel(
        ISettingsService settings,
        IWallpaperRotationService rotation,
        IWallpaperScheduler scheduler,
        ILogService log,
        IBackgroundScheduler background,
        IDeviceDisplayService display,
        IWallpaperSetter setter)
    {
        _settings = settings;
        _rotation = rotation;
        _scheduler = scheduler;
        _log = log;
        _background = background;
        _display = display;
        _setter = setter;

        _rotation.SwitchCompleted += (_, result) =>
            MainThread.BeginInvokeOnMainThread(() => StatusMessage = result.Message);

        _scheduler.NextSwitchTimeChanged += (_, _) =>
            MainThread.BeginInvokeOnMainThread(RefreshScheduleText);
    }

    /// <summary>当前壁纸预览（从磁盘读取为内存流，避免占用文件锁影响后续覆盖写入）。</summary>
    [ObservableProperty]
    public partial ImageSource? WallpaperSource { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "准备就绪";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    public partial bool IsBusy { get; set; }

    /// <summary>是否允许触发“立即切换”等操作（忙碌时禁用按钮）。</summary>
    public bool CanInteract => !IsBusy;

    /// <summary>是否启用自动切换（关闭后仍可用“立即切换”）。</summary>
    [ObservableProperty]
    public partial bool IsAutomaticEnabled { get; set; }

    [ObservableProperty]
    public partial string NextSwitchText { get; set; } = "-";

    [ObservableProperty]
    public partial string LastSwitchText { get; set; } = "尚未切换过";

    [ObservableProperty]
    public partial string IntervalText { get; set; } = "-";

    [ObservableProperty]
    public partial string CategoriesText { get; set; } = "未选择任何类别";

    [ObservableProperty]
    public partial string LastWallpaperText { get; set; } = "-";

    [ObservableProperty]
    public partial string SourceUrlText { get; set; } = "-";

    [ObservableProperty]
    public partial string PlatformText { get; set; } = "-";

    [ObservableProperty]
    public partial string LockScreenText { get; set; } = "-";

    [ObservableProperty]
    public partial string BackgroundText { get; set; } = "-";

    /// <summary>页面显示时刷新全部信息。</summary>
    public async Task RefreshAsync()
    {
        var settings = _settings.Current;

        _suppressToggle = true;
        IsAutomaticEnabled = settings.IsEnabled;
        _suppressToggle = false;

        IntervalText = settings.Interval.ToDisplayName();
        CategoriesText = settings.SelectedCategories.Count == 0
            ? "未选择任何类别（请先到“类别”页选择）"
            : string.Join("、", settings.SelectedCategories);

        LastSwitchText = settings.LastSwitchTime is { } last
            ? last.ToString("yyyy-MM-dd HH:mm:ss")
            : "尚未切换过";

        LastWallpaperText = string.IsNullOrWhiteSpace(settings.LastWallpaperCategory)
            ? "-"
            : $"{settings.LastWallpaperCategory} · {settings.LastWallpaperProvider}";

        SourceUrlText = string.IsNullOrWhiteSpace(settings.LastWallpaperSourceUrl)
            ? "-"
            : settings.LastWallpaperSourceUrl!;

        PlatformText = $"{_setter.PlatformName} · {_display.DeviceDescription}";
        LockScreenText = _setter.SupportsLockScreen
            ? "支持同时设置桌面壁纸和锁屏壁纸"
            : "该平台不支持设置锁屏壁纸（仅设置桌面壁纸）";
        BackgroundText = _background.Description;

        RefreshScheduleText();
        await UpdatePreviewAsync().ConfigureAwait(true);
    }

    /// <summary>“立即切换”：忽略定时设置，立即执行一次切换流程（需求 3.5）。</summary>
    [RelayCommand]
    private async Task SwitchNowAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在获取高清壁纸…";

        try
        {
            var result = await _rotation.SwitchNowAsync(isAutomatic: false).ConfigureAwait(true);
            StatusMessage = result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"切换失败：{ex.Message}";
            _log.Write(WallpaperLogLevel.Error, "立即切换失败", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>使用本地缓存中的历史壁纸（断网时的备选，需求 3.3.3）。</summary>
    [RelayCommand]
    private async Task UseCachedAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _rotation.SwitchToCachedAsync().ConfigureAwait(true);
            StatusMessage = result.Message;
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"使用缓存壁纸失败：{ex.Message}";
            _log.Write(WallpaperLogLevel.Error, "使用缓存壁纸失败", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>用户切换“启用自动切换”开关。</summary>
    partial void OnIsAutomaticEnabledChanged(bool value)
    {
        if (_suppressToggle)
        {
            return;
        }

        _ = ApplyEnabledAsync(value);
    }

    private async Task ApplyEnabledAsync(bool enabled)
    {
        try
        {
            var settings = _settings.Current;
            settings.IsEnabled = enabled;
            await _settings.SaveAsync(settings).ConfigureAwait(true);

            if (enabled)
            {
                await _background.StartAsync().ConfigureAwait(true);
                await _scheduler.RescheduleAsync().ConfigureAwait(true);
                _log.Write(WallpaperLogLevel.Info, "已启用自动切换");
                StatusMessage = "已启用自动切换";
            }
            else
            {
                await _background.StopAsync().ConfigureAwait(true);
                _log.Write(WallpaperLogLevel.Info, "已暂停自动切换（“立即切换”仍然可用）");
                StatusMessage = "已暂停自动切换（“立即切换”仍然可用）";
            }

            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"设置自动切换失败：{ex.Message}";
            _log.Write(WallpaperLogLevel.Error, "设置自动切换状态失败", ex, source: "应用");
        }
    }

    private void RefreshScheduleText()
    {
        var settings = _settings.Current;

        if (!settings.IsEnabled)
        {
            NextSwitchText = "自动切换已暂停";
            return;
        }

        if (settings.Interval == SwitchInterval.Never)
        {
            NextSwitchText = "“永久”模式：不自动切换（可手动切换）";
            return;
        }

        var next = _scheduler.NextSwitchTime ?? settings.NextSwitchTime;
        if (next is null)
        {
            NextSwitchText = "尚未安排（完成一次切换后自动计算）";
            return;
        }

        var remaining = next.Value - DateTime.Now;
        var remainingText = remaining > TimeSpan.Zero
            ? $"（还有 {FormatRemaining(remaining)}）"
            : "（即将执行）";

        NextSwitchText = $"{next.Value:yyyy-MM-dd HH:mm}{remainingText}";
    }

    private static string FormatRemaining(TimeSpan span)
    {
        if (span.TotalDays >= 1)
        {
            return $"{(int)span.TotalDays} 天 {span.Hours} 小时";
        }

        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours} 小时 {span.Minutes} 分钟";
        }

        return $"{span.Minutes} 分钟";
    }

    private async Task UpdatePreviewAsync()
    {
        var path = _settings.Current.LastWallpaperPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            WallpaperSource = null;
            return;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(true);

            // 用内存流展示，避免预览控件长期占用 current.jpg 造成后续写入失败。
            WallpaperSource = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch (Exception ex)
        {
            WallpaperSource = null;
            _log.Write(WallpaperLogLevel.Warning, "读取当前壁纸预览失败", ex);
        }
    }
}
