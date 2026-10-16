using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;

namespace DailyWallpaper.ViewModels;

/// <summary>权限引导页中的一项（可观察，便于刷新完成状态）。</summary>
public partial class PermissionItemViewModel : ObservableObject
{
    public PermissionItemViewModel(PermissionGuideItem item)
    {
        Id = item.Id;
        Title = item.Title;
        Description = item.Description;
        ActionText = item.ActionText;
        IsCompleted = item.IsCompleted;
    }

    public string Id { get; }

    public string Title { get; }

    public string Description { get; }

    public string ActionText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    public partial bool IsCompleted { get; set; }

    public string StateText => IsCompleted ? "已完成" : "待设置";
}

/// <summary>
/// 权限引导页（需求 5.4 / 3.3.1）：自启动、电池优化白名单、省电策略、通知、精确闹钟、后台锁定。
/// </summary>
public partial class PermissionsViewModel : ObservableObject
{
    private readonly IPermissionGuideService _permissions;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;

    public PermissionsViewModel(
        IPermissionGuideService permissions,
        ISettingsService settings,
        ILogService log)
    {
        _permissions = permissions;
        _settings = settings;
        _log = log;
    }

    public ObservableCollection<PermissionItemViewModel> Items { get; } = new();

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSupported { get; set; }

    /// <summary>当前平台不需要权限引导（Windows）。</summary>
    public bool IsNotSupported => !IsSupported;

    /// <summary>Android 无法保证 100% 不被系统杀掉，界面上必须如实说明。</summary>
    public string Disclaimer =>
        "提示：在没有 Root 的手机上，任何应用都无法保证 100% 不被系统关闭。"
        + "完成上面的设置后，本应用会“极难被杀死”，但厂商的省电策略、极端低电量等情况下系统仍可能终止它。"
        + "如果发现壁纸不再自动更换，请回到应用确认前台服务是否还在运行。";

    public string NonAndroidNote =>
        "当前平台不需要这些引导项：Windows 端已通过系统托盘常驻，并在注册表中登记开机自启。";

    /// <summary>加载引导项与状态。</summary>
    public async Task LoadAsync()
    {
        IsSupported = _permissions.IsSupported;
        Items.Clear();

        var items = await _permissions.GetItemsAsync().ConfigureAwait(true);
        foreach (var item in items)
        {
            Items.Add(new PermissionItemViewModel(item));
        }

        OnPropertyChanged(nameof(IsNotSupported));
    }

    /// <summary>执行引导动作（跳转到对应的系统设置页）。</summary>
    [RelayCommand]
    private async Task ExecuteAsync(PermissionItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            var handled = await _permissions.ExecuteAsync(item.Id).ConfigureAwait(true);

            Message = handled
                ? $"已打开“{item.Title}”对应的系统设置页，请按说明完成设置。"
                : $"系统未提供“{item.Title}”的跳转入口，请手动在系统设置中完成。";
        }
        catch (Exception ex)
        {
            Message = $"跳转失败：{ex.Message}";
            _log.Write(Models.WallpaperLogLevel.Error, $"权限引导跳转失败：{item.Id}", ex);
        }
    }

    /// <summary>重新检测各项状态（用户从系统设置页返回后点击）。</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync().ConfigureAwait(true);
        Message = "已重新检测权限状态。";
    }

    /// <summary>标记引导已完成。</summary>
    [RelayCommand]
    private async Task CompleteAsync()
    {
        var settings = _settings.Current;
        settings.PermissionGuideCompleted = true;
        await _settings.SaveAsync(settings).ConfigureAwait(true);

        Message = "已记录：权限引导完成。";
    }
}
