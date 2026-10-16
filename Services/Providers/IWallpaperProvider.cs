using DailyWallpaper.Models;

namespace DailyWallpaper.Services;

/// <summary>
/// 壁纸源抽象（需求 3.2）：便于后续替换或新增图片 API。
/// </summary>
public interface IWallpaperProvider
{
    /// <summary>壁纸源名称，用于日志与界面展示。</summary>
    string Name { get; }

    /// <summary>是否支持该查询（例如是否支持指定关键词）。</summary>
    bool CanHandle(WallpaperQuery query);

    /// <summary>
    /// 随机获取一张候选壁纸信息；失败或没有结果时返回 <c>null</c>（不抛异常，由调用方尝试下一个源）。
    /// </summary>
    Task<WallpaperCandidate?> GetRandomAsync(WallpaperQuery query, CancellationToken cancellationToken = default);
}
