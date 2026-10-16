using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyWallpaper.Models;
using DailyWallpaper.Services;

namespace DailyWallpaper.ViewModels;

/// <summary>
/// 类别设置页（需求 3.1 / 5.2）：
/// 默认类别只能勾选 / 取消，自定义类别可增删，所有修改通过“保存”按钮落盘。
/// </summary>
public partial class CategoriesViewModel : ObservableObject
{
    private readonly ICategoryService _categories;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;

    /// <summary>刷新列表时抑制勾选数量校验，避免误报。</summary>
    private bool _suppressSelectionGuard;

    public CategoriesViewModel(ICategoryService categories, ISettingsService settings, ILogService log)
    {
        _categories = categories;
        _settings = settings;
        _log = log;
    }

    /// <summary>默认类别 + 自定义类别（默认类别在前）。</summary>
    public ObservableCollection<CategoryItem> Items { get; } = new();

    [ObservableProperty]
    public partial string NewCategoryName { get; set; } = string.Empty;

    /// <summary>校验结果 / 操作提示。</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>提示是否为错误（用于界面配色）。</summary>
    [ObservableProperty]
    public partial bool IsError { get; set; }

    [ObservableProperty]
    public partial string CounterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>类别配置文件路径，便于用户直接编辑关键词映射。</summary>
    public string ConfigFilePath => _categories.ConfigFilePath;

    /// <summary>名称长度上限，用于界面提示。</summary>
    public int MaxNameLength => Defaults.MaxCategoryNameLength;

    /// <summary>可同时启用的类别数量上限。</summary>
    public int MaxCategories => Defaults.MaxSelectedCategories;

    /// <summary>加载（或重新加载）类别列表。</summary>
    public async Task LoadAsync()
    {
        _suppressSelectionGuard = true;
        try
        {
            Items.Clear();
            var items = await _categories.BuildItemsAsync().ConfigureAwait(true);
            foreach (var item in items)
            {
                item.PropertyChanged += OnItemPropertyChanged;
                Items.Add(item);
            }
        }
        finally
        {
            _suppressSelectionGuard = false;
        }

        UpdateCounter();
    }

    /// <summary>添加自定义类别（需求 3.1.3：长度、重复、包含关系校验）。</summary>
    [RelayCommand]
    private async Task AddAsync()
    {
        var input = NewCategoryName;

        var result = await _categories
            .AddCustomCategoryAsync(input, Items.Select(item => item.Name))
            .ConfigureAwait(true);

        if (!result.IsValid)
        {
            IsError = true;
            Message = result.ErrorMessage ?? "类别名称不符合要求。";
            return;
        }

        NewCategoryName = string.Empty;
        IsError = false;
        Message = $"已添加类别“{input?.Trim()}”。";
        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>删除自定义类别（默认类别不显示删除入口）。</summary>
    [RelayCommand]
    private async Task DeleteAsync(CategoryItem? item)
    {
        if (item is null || item.IsDefault)
        {
            return;
        }

        await _categories.DeleteCustomCategoryAsync(item.Name).ConfigureAwait(true);
        IsError = false;
        Message = $"已删除自定义类别“{item.Name}”。";
        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>重置壁纸类别选项：删除全部自定义类别，恢复默认预设并全部勾选（需求 3.1.2）。</summary>
    [RelayCommand]
    private async Task ResetAsync()
    {
        await _categories.ResetToDefaultsAsync().ConfigureAwait(true);
        IsError = false;
        Message = "已重置为默认预设类别（全部勾选）。";
        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>保存勾选结果（需求 3.1.3：至少选择 1 个，最多 9 个）。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var selected = Items.Where(item => item.IsSelected).Select(item => item.Name).ToList();

        if (selected.Count < Defaults.MinSelectedCategories)
        {
            IsError = true;
            Message = "至少需要选择 1 个类别，否则无法保存。";
            return;
        }

        if (selected.Count > Defaults.MaxSelectedCategories)
        {
            IsError = true;
            Message = $"最多只能同时启用 {Defaults.MaxSelectedCategories} 个类别。";
            return;
        }

        var settings = _settings.Current;
        settings.SelectedCategories = selected;
        await _settings.SaveAsync(settings).ConfigureAwait(true);

        _log.Write(WallpaperLogLevel.Info, $"已保存壁纸类别：{string.Join("、", selected)}");
        IsError = false;
        Message = $"已保存，共启用 {selected.Count} 个类别。";
        UpdateCounter();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CategoryItem.IsSelected))
        {
            return;
        }

        if (_suppressSelectionGuard || sender is not CategoryItem item)
        {
            return;
        }

        if (item.IsSelected && Items.Count(entry => entry.IsSelected) > Defaults.MaxSelectedCategories)
        {
            _suppressSelectionGuard = true;
            item.IsSelected = false;
            _suppressSelectionGuard = false;

            IsError = true;
            Message = $"最多只能同时启用 {Defaults.MaxSelectedCategories} 个类别，已取消勾选“{item.Name}”。";
        }

        UpdateCounter();
    }

    private void UpdateCounter()
    {
        var selected = Items.Count(item => item.IsSelected);
        CounterText = $"已启用 {selected} / {Defaults.MaxSelectedCategories} 个类别　"
                      + $"（默认 {Items.Count(item => item.IsDefault)} 个，自定义 {Items.Count(item => !item.IsDefault)} 个）";
    }
}
