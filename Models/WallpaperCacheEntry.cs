namespace DailyWallpaper.Models;

/// <summary>
/// 本地壁纸缓存条目（需求 3.3.3：缓存最近若干张壁纸，避免重复下载并作为断网备选）。
/// </summary>
public sealed class WallpaperCacheEntry
{
    /// <summary>缓存文件名（位于缓存目录内）。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>去重标识（壁纸源提供的唯一键，通常是图片 URL）。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>该壁纸所属类别。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>壁纸源名称。</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>原始图片地址。</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>缓存时间。</summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;
}

/// <summary>缓存索引文件结构。</summary>
public sealed class WallpaperCacheIndex
{
    public List<WallpaperCacheEntry> Entries { get; set; } = new();
}
