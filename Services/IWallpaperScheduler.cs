using DailyWallpaper.Models;

namespace DailyWallpaper.Services;

/// <summary>
/// 定时调度（需求 3.3 / 3.4）：按上次切换时间与间隔计算下一个时间点并触发切换。
/// </summary>
public interface IWallpaperScheduler
{
    /// <summary>下一次计划切换时间（“永久”模式为 <c>null</c>）。</summary>
    DateTime? NextSwitchTime { get; }

    /// <summary>下次切换时间变化时触发（界面据此刷新显示）。</summary>
    event EventHandler<DateTime?>? NextSwitchTimeChanged;

    /// <summary>应用启动 / 服务启动时调用：做一次过期补切检查并重新安排定时。</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>设置变化后重新安排定时。</summary>
    Task RescheduleAsync(CancellationToken cancellationToken = default);

    /// <summary>如果已到达（或已错过）切换时间，立即执行一次自动切换。</summary>
    Task<bool> RunIfDueAsync(CancellationToken cancellationToken = default);
}
