using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyWallpaper.Models;
using DailyWallpaper.Services;

namespace DailyWallpaper.ViewModels;

/// <summary>切换时间设置页中的单个可选项。</summary>
public partial class IntervalOptionItem : ObservableObject
{
    public IntervalOptionItem(SwitchInterval interval, bool isSelected)
    {
        Interval = interval;
        DisplayName = interval.ToDisplayName();
        Description = interval switch
        {
            SwitchInterval.SixHours => "每天自动更换 4 次。",
            SwitchInterval.TwelveHours => "每天自动更换 2 次。",
            SwitchInterval.OneDay => "默认方案，每天自动更换 1 次。",
            SwitchInterval.OneWeek => "每周自动更换 1 次，适合喜欢长期稳定壁纸的用户。",
            _ => "不自动切换；仍可在主页使用“立即切换”。"
        };
        IsSelected = isSelected;
    }

    public SwitchInterval Interval { get; }

    public string DisplayName { get; }

    public string Description { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// 切换时间设置页（需求 3.4 / 5.3）：6 小时 / 12 小时 / 一天 / 一星期 / 永久。
/// </summary>
public partial class IntervalViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IWallpaperScheduler _scheduler;
    private readonly ILogService _log;

    public IntervalViewModel(ISettingsService settings, IWallpaperScheduler scheduler, ILogService log)
    {
        _settings = settings;
        _scheduler = scheduler;
        _log = log;

        foreach (var interval in SwitchIntervalExtensions.All)
        {
            Options.Add(new IntervalOptionItem(interval, false));
        }
    }

    public ObservableCollection<IntervalOptionItem> Options { get; } = new();

    /// <summary>当前选中的间隔（CollectionView 单选绑定）。</summary>
    [ObservableProperty]
    public partial IntervalOptionItem? SelectedOption { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NextSwitchText { get; set; } = "-";

    [ObservableProperty]
    public partial bool IsError { get; set; }

    /// <summary>加载当前设置。</summary>
    public Task LoadAsync()
    {
        var current = _settings.Current.Interval;
        SelectedOption = Options.FirstOrDefault(option => option.Interval == current) ?? Options[0];

        RefreshNextSwitchText();
        return Task.CompletedTask;
    }

    /// <summary>保存选中的间隔并重新安排定时任务（应用重启 / 开机后同样会重新计算）。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var selected = SelectedOption;
        if (selected is null)
        {
            IsError = true;
            Message = "请先选择一个切换时间。";
            return;
        }

        var settings = _settings.Current;
        settings.Interval = selected.Interval;
        await _settings.SaveAsync(settings).ConfigureAwait(true);

        // 立即按新间隔重新计算下一次切换时间。
        await _scheduler.RescheduleAsync().ConfigureAwait(true);

        _log.Write(WallpaperLogLevel.Info, $"已设置自动切换间隔：{selected.DisplayName}");

        IsError = false;
        Message = selected.Interval == SwitchInterval.Never
            ? "已设置为“永久”：不会自动切换壁纸，仍可使用“立即切换”。"
            : $"已保存：{selected.DisplayName}";

        RefreshNextSwitchText();
    }

    /// <summary>由选项的 IsSelected 变化触发（通过 Pages 的 CheckedChanged 或点击命令调用）。</summary>
    public void SelectInterval(IntervalOptionItem item)
    {
        SelectedOption = item;
    }

    /// <summary>保持“单选”语义：选中项之外的选项全部取消选中。</summary>
    partial void OnSelectedOptionChanged(IntervalOptionItem? value)
    {
        foreach (var option in Options)
        {
            option.IsSelected = ReferenceEquals(option, value);
        }

        RefreshNextSwitchText();
    }

    private void RefreshNextSwitchText()
    {
        var settings = _settings.Current;

        if (settings.Interval == SwitchInterval.Never && !Options.Any(option => option.IsSelected))
        {
            NextSwitchText = "当前为“永久”模式，不会自动切换。";
            return;
        }

        var next = _scheduler.NextSwitchTime ?? settings.NextSwitchTime;
        NextSwitchText = next is null
            ? "下一次切换时间将在保存或完成一次切换后计算。"
            : $"下一次计划切换：{next.Value:yyyy-MM-dd HH:mm}";
    }
}
