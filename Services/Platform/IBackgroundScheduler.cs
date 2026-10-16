using DailyWallpaper.Models;

namespace DailyWallpaper.Services.Platform;

/// <summary>
/// 平台后台常驻与定时调度能力。
/// Android：前台服务 + AlarmManager；Windows：常驻托盘 + 定时器。
/// </summary>
public interface IBackgroundScheduler
{
    /// <summary>当前平台是否支持后台常驻（Android 支持；Windows 依赖托盘实现）。</summary>
    bool IsSupported { get; }

    /// <summary>给用户看的后台常驻说明（平台差异较大）。</summary>
    string Description { get; }

    /// <summary>后台任务当前是否处于运行状态。</summary>
    Task<bool> IsRunningAsync(CancellationToken cancellationToken = default);

    /// <summary>启动后台常驻（Android 启动前台服务，Windows 启动内部定时器）。</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>停止后台常驻。</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>根据下一次切换时间设置闹钟 / 定时器；<paramref name="nextTime"/> 为 <c>null</c> 表示“永久”模式，取消定时。</summary>
    Task ScheduleNextAsync(DateTime? nextTime, SwitchInterval interval, CancellationToken cancellationToken = default);
}
