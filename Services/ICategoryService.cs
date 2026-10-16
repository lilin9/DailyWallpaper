using DailyWallpaper.Models;

namespace DailyWallpaper.Services;

/// <summary>
/// 类别管理：默认类别来自 <c>categories.json</c>，自定义类别保存在设置中。
/// </summary>
public interface ICategoryService
{
    /// <summary>类别配置文件路径（用户可直接编辑该文件增改默认类别与关键词）。</summary>
    string ConfigFilePath { get; }

    /// <summary>获取类别配置（首次运行会从安装包内释放默认配置到应用数据目录）。</summary>
    Task<WallpaperCategoryConfig> GetConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>重新从磁盘加载配置（用户手动编辑文件后调用）。</summary>
    Task<WallpaperCategoryConfig> ReloadConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>构建界面使用的类别列表（默认类别 + 自定义类别，含勾选状态）。</summary>
    Task<IReadOnlyList<CategoryItem>> BuildItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>把类别名称映射为搜索关键词；未配置时直接使用类别名称。</summary>
    Task<string> ResolveKeywordAsync(string categoryName, CancellationToken cancellationToken = default);

    /// <summary>校验用户输入的新类别名称（不落盘）。</summary>
    CategoryValidationResult ValidateNewCategoryName(string? input, IEnumerable<string> existingNames);

    /// <summary>添加自定义类别（含全部校验）并保存设置。</summary>
    Task<CategoryValidationResult> AddCustomCategoryAsync(
        string? input,
        IEnumerable<string> existingNames,
        CancellationToken cancellationToken = default);

    /// <summary>删除自定义类别并保存设置。</summary>
    Task DeleteCustomCategoryAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>重置壁纸类别选项：删除全部自定义类别，恢复为全部默认类别且全部勾选。</summary>
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);
}
