using DailyWallpaper.Models;

namespace DailyWallpaper.Services;

/// <summary>
/// 壁纸本地缓存：保存最近处理好的图片，用于去重与断网兜底。
/// </summary>
public interface IWallpaperCache
{
    /// <summary>缓存目录。</summary>
    string CacheDirectory { get; }

    /// <summary>最近使用过的图片标识（新的在前），传给壁纸源以避免连续重复。</summary>
    IReadOnlyList<string> RecentKeys { get; }

    /// <summary>把处理好的图片写入缓存，返回完整文件路径。</summary>
    Task<string> StoreAsync(
        byte[] processedImage,
        WallpaperCandidate candidate,
        CancellationToken cancellationToken = default);

    /// <summary>从缓存中随机取一张历史壁纸（断网时兜底）；没有可用缓存时返回 <c>null</c>。</summary>
    Task<WallpaperCacheEntry?> GetRandomAsync(CancellationToken cancellationToken = default);

    /// <summary>按缓存上限清理最旧的条目。</summary>
    Task CleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>清空缓存。</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
