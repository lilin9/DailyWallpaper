using CommunityToolkit.Mvvm.ComponentModel;

namespace DailyWallpaper.Models;

/// <summary>
/// <c>Resources/Raw/categories.json</c> 反序列化后的结构。
/// 用户可以编辑 <c>AppDataDirectory/categories.json</c> 来增删默认类别或修改搜索关键词。
/// </summary>
public sealed class WallpaperCategoryConfig
{
    /// <summary>默认预设类别（不可删除，只能取消勾选）。</summary>
    public List<CategoryDefinition> DefaultCategories { get; set; } = new();

    /// <summary>
    /// 为每个类别查找搜索关键词；未配置时返回 <c>null</c>，调用方会退化为直接使用类别名称作为关键词。
    /// </summary>
    public string? FindKeyword(string categoryName)
    {
        foreach (var item in DefaultCategories)
        {
            if (string.Equals(item.Name, categoryName, StringComparison.Ordinal))
            {
                return item.Keyword;
            }
        }

        return null;
    }
}

/// <summary>单个默认类别定义：显示名称 + 网络搜索关键词。</summary>
public sealed class CategoryDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Keyword { get; set; } = string.Empty;
}

/// <summary>
/// 类别页使用的可观察类别项（默认类别 + 自定义类别）。
/// </summary>
public partial class CategoryItem : ObservableObject
{
    public CategoryItem(string name, string keyword, bool isDefault, bool isSelected)
    {
        Name = name;
        Keyword = keyword;
        IsDefault = isDefault;
        IsSelected = isSelected;
    }

    /// <summary>类别显示名称。</summary>
    public string Name { get; }

    /// <summary>映射到壁纸源的搜索关键词。</summary>
    public string Keyword { get; }

    /// <summary>是否属于默认预设类别（默认类别不可修改、不可删除）。</summary>
    public bool IsDefault { get; }

    /// <summary>自定义类别才允许删除。</summary>
    public bool CanDelete => !IsDefault;

    /// <summary>列表右侧的标签文本。</summary>
    public string SourceLabel => IsDefault ? "默认" : "自定义";

    /// <summary>是否被勾选（参与随机切换）。</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
