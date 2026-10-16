using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyWallpaper.Models;
using DailyWallpaper.Services;

namespace DailyWallpaper.ViewModels;

/// <summary>
/// 日志页（需求 5.6）：记录切换成功 / 失败、网络异常、权限异常等，支持按级别筛选与清空。
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private const string FilterAll = "全部";
    private const string FilterSuccess = "成功";
    private const string FilterWarning = "警告";
    private const string FilterError = "错误";

    private readonly ILogService _log;
    private readonly List<WallpaperLogEntry> _all = new();

    public LogsViewModel(ILogService log)
    {
        _log = log;

        _log.EntryAdded += (_, entry) => MainThread.BeginInvokeOnMainThread(() =>
        {
            _all.Insert(0, entry);
            if (Matches(entry))
            {
                Entries.Insert(0, entry);
            }

            UpdateSummary();
        });
    }

    /// <summary>筛选后的日志（新的在前）。</summary>
    public ObservableCollection<WallpaperLogEntry> Entries { get; } = new();

    public IList<string> FilterOptions { get; } = new List<string>
    {
        FilterAll, FilterSuccess, FilterWarning, FilterError
    };

    [ObservableProperty]
    public partial string SelectedFilter { get; set; } = FilterAll;

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "暂无日志";

    public string LogFilePath => _log.LogFilePath;

    /// <summary>页面显示时重新载入日志。</summary>
    public async Task LoadAsync()
    {
        await _log.LoadAsync().ConfigureAwait(true);

        _all.Clear();
        _all.AddRange(_log.Entries);

        ApplyFilter();
    }

    /// <summary>重新加载日志（下拉刷新按钮）。</summary>
    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    /// <summary>清空全部日志。</summary>
    [RelayCommand]
    private async Task ClearAsync()
    {
        await _log.ClearAsync().ConfigureAwait(true);
        _all.Clear();
        ApplyFilter();
    }

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Entries.Clear();

        foreach (var entry in _all)
        {
            if (Matches(entry))
            {
                Entries.Add(entry);
            }
        }

        UpdateSummary();
    }

    private bool Matches(WallpaperLogEntry entry) => SelectedFilter switch
    {
        FilterSuccess => entry.Level == WallpaperLogLevel.Success,
        FilterWarning => entry.Level == WallpaperLogLevel.Warning,
        FilterError => entry.Level == WallpaperLogLevel.Error,
        _ => true
    };

    private void UpdateSummary()
    {
        var failures = _all.Count(entry => entry.Level == WallpaperLogLevel.Error);
        SummaryText = _all.Count == 0
            ? "暂无日志"
            : $"共 {_all.Count} 条记录，其中错误 {failures} 条（当前显示 {Entries.Count} 条）";
    }
}
