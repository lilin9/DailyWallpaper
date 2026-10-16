using DailyWallpaper.Models;

namespace DailyWallpaper.Services;

/// <summary>
/// 壁纸切换核心流程（需求 3.2 / 3.5）：随机类别 → 获取图片 → 缩放裁剪 → 设置壁纸 → 更新计划时间。
/// </summary>
public interface IWallpaperRotationService
{
    /// <summary>每次切换流程结束后触发（包括失败）。</summary>
    event EventHandler<WallpaperSwitchResult>? SwitchCompleted;

    /// <summary>是否正在执行切换，避免定时任务与“立即切换”并发执行。</summary>
    bool IsBusy { get; }

    /// <summary>当前缓存的壁纸文件名（用于界面显示，可能为 <c>null</c>）。</summary>
    string? CurrentWallpaperPath { get; }

    /// <summary>
    /// 立即执行一次壁纸切换。
    /// </summary>
    /// <param name="isAutomatic">是否为定时任务触发（定时任务在“未启用”或“永久”模式下会跳过）。</param>
    Task<WallpaperSwitchResult> SwitchNowAsync(bool isAutomatic, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用本地缓存中的一张历史壁纸（断网兜底，需求 3.3.3），不会访问网络。
    /// </summary>
    Task<WallpaperSwitchResult> SwitchToCachedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据上次切换时间与间隔计算下一次切换时间；
    /// “永久”模式或参数非法时返回 <c>null</c>；若计算结果已过期，则顺延到未来最近的时间点。
    /// </summary>
    DateTime? ComputeNextSwitchTime(AppSettings settings, DateTime now);
}
