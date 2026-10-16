using DailyWallpaper.Models;
using DailyWallpaper.Services.Platform;
using Microsoft.Extensions.Logging;

namespace DailyWallpaper.Services;

/// <summary>
/// 共享的定时器实现：Android 与 Windows 都用它来触发“到点切换”，
/// 同时通过 <see cref="IBackgroundScheduler"/> 让 Android 额外注册系统闹钟与前台服务。
/// </summary>
public sealed class WallpaperScheduler : IWallpaperScheduler, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly IWallpaperRotationService _rotation;
    private readonly IBackgroundScheduler _background;
    private readonly ILogService _log;
    private readonly ILogger<WallpaperScheduler> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Timer? _timer;
    private bool _disposed;

    public WallpaperScheduler(
        ISettingsService settings,
        IWallpaperRotationService rotation,
        IBackgroundScheduler background,
        ILogService log,
        ILogger<WallpaperScheduler> logger)
    {
        _settings = settings;
        _rotation = rotation;
        _background = background;
        _log = log;
        _logger = logger;
    }

    /// <inheritdoc />
    public DateTime? NextSwitchTime { get; private set; }

    /// <inheritdoc />
    public event EventHandler<DateTime?>? NextSwitchTimeChanged;

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;

        // 应用重启 / 开机后：根据上次切换时间重新计算下一次切换时间（需求 3.4）。
        settings.NextSwitchTime = _rotation.ComputeNextSwitchTime(settings, DateTime.Now);
        await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);

        await RescheduleAsync(cancellationToken).ConfigureAwait(false);
        await RunIfDueAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RescheduleAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;
        var next = _rotation.ComputeNextSwitchTime(settings, DateTime.Now);

        if (settings.NextSwitchTime != next)
        {
            settings.NextSwitchTime = next;
            await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        NextSwitchTime = next;
        NextSwitchTimeChanged?.Invoke(this, next);

        ScheduleTimer(next);

        // Android 端同时注册系统闹钟（受系统限制，仍可能被延迟或拦截）。
        await _background
            .ScheduleNextAsync(settings.IsEnabled ? next : null, settings.Interval, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RunIfDueAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;

        if (!settings.IsEnabled || settings.Interval == SwitchInterval.Never)
        {
            return false;
        }

        var next = settings.NextSwitchTime;
        if (next is null || next.Value > DateTime.Now)
        {
            return false;
        }

        _log.Write(
            WallpaperLogLevel.Info,
            $"到达计划切换时间（{next.Value:yyyy-MM-dd HH:mm}），开始自动切换",
            source: _background.IsSupported ? "后台服务" : "应用内定时器");

        await _rotation.SwitchNowAsync(isAutomatic: true, cancellationToken).ConfigureAwait(false);
        await RescheduleAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer?.Dispose();
        _timer = null;
        _gate.Dispose();
    }

    /// <summary>按下一个时间点设置一次性定时器，避免长时间运行时的累积漂移。</summary>
    private void ScheduleTimer(DateTime? next)
    {
        if (_disposed)
        {
            return;
        }

        _timer?.Dispose();
        _timer = null;

        if (next is null)
        {
            return;
        }

        var due = next.Value - DateTime.Now;
        if (due <= TimeSpan.Zero)
        {
            due = TimeSpan.FromSeconds(5);
        }

        // Timer 的上限约为 49.7 天，一星期间隔完全够用。
        _timer = new Timer(OnTimerTick, null, due, Timeout.InfiniteTimeSpan);

        _logger.LogDebug("下一次自动切换时间：{Next}（{Due} 后）", next.Value, due);
    }

    private void OnTimerTick(object? state)
    {
        // 定时器回调在工作线程上执行，需要保证“到点切换 + 重新排程”串行完成。
        _ = Task.Run(async () =>
        {
            if (!await _gate.WaitAsync(TimeSpan.Zero).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                await RunIfDueAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Write(WallpaperLogLevel.Error, "定时切换失败", ex);
            }
            finally
            {
                _gate.Release();
            }
        });
    }
}
