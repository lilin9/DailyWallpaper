using DailyWallpaper.Models;
using DailyWallpaper.Services.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace DailyWallpaper.Services;

/// <summary>
/// <see cref="IWallpaperRotationService"/> 的默认实现。
/// </summary>
/// <remarks>
/// 失败处理遵循需求 3.3.3：网络失败时保持当前壁纸不变、跳过本次切换、写入日志，
/// 并把下一次切换时间推到未来的预设时间点（不会因网络恢复而立即切换）。
/// </remarks>
public sealed class WallpaperRotationService : IWallpaperRotationService
{
    /// <summary>重新使用过的图片标识保留数量（避免连续切换重复）。</summary>
    private const int RecentKeyLimit = 40;

    private readonly ISettingsService _settings;
    private readonly ICategoryService _categories;
    private readonly IWallpaperDownloader _downloader;
    private readonly IImageProcessor _imageProcessor;
    private readonly IWallpaperCache _cache;
    private readonly IWallpaperSetter _setter;
    private readonly IDeviceDisplayService _display;
    private readonly IEnumerable<IWallpaperProvider> _providers;
    private readonly ILogService _log;
    private readonly ILogger<WallpaperRotationService> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public WallpaperRotationService(
        ISettingsService settings,
        ICategoryService categories,
        IWallpaperDownloader downloader,
        IImageProcessor imageProcessor,
        IWallpaperCache cache,
        IWallpaperSetter setter,
        IDeviceDisplayService display,
        IEnumerable<IWallpaperProvider> providers,
        ILogService log,
        ILogger<WallpaperRotationService> logger)
    {
        _settings = settings;
        _categories = categories;
        _downloader = downloader;
        _imageProcessor = imageProcessor;
        _cache = cache;
        _setter = setter;
        _display = display;
        _providers = providers;
        _log = log;
        _logger = logger;

        // 处理后的壁纸统一放在固定文件里：Windows 的 SystemParametersInfo 需要一个长期有效的绝对路径。
        var directory = Path.Combine(FileSystem.AppDataDirectory, "current");
        Directory.CreateDirectory(directory);
        CurrentWallpaperPath = Path.Combine(directory, "current.jpg");
    }

    /// <inheritdoc />
    public event EventHandler<WallpaperSwitchResult>? SwitchCompleted;

    /// <inheritdoc />
    public bool IsBusy { get; private set; }

    /// <inheritdoc />
    public string? CurrentWallpaperPath { get; }

    /// <inheritdoc />
    public async Task<WallpaperSwitchResult> SwitchNowAsync(bool isAutomatic, CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
        {
            return new WallpaperSwitchResult(false, "已有切换任务正在执行，请稍候再试。");
        }

        try
        {
            IsBusy = true;
            return await RunSwitchAsync(isAutomatic, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new WallpaperSwitchResult(false, "切换已取消。");
        }
        catch (Exception ex)
        {
            _log.Write(WallpaperLogLevel.Error, "切换壁纸时发生未预期的异常", ex);
            _logger.LogError(ex, "切换壁纸失败");

            var settings = _settings.Current;
            AdvanceNextSwitchTime(settings);
            await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);

            return new WallpaperSwitchResult(false, $"切换失败：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<WallpaperSwitchResult> SwitchToCachedAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
        {
            return new WallpaperSwitchResult(false, "已有切换任务正在执行，请稍候再试。");
        }

        try
        {
            IsBusy = true;

            var entry = await _cache.GetRandomAsync(cancellationToken).ConfigureAwait(false);
            if (entry is null)
            {
                return new WallpaperSwitchResult(false, "本地还没有缓存壁纸，请先成功切换一次。");
            }

            var path = Path.Combine(_cache.CacheDirectory, entry.FileName);
            var setResult = await _setter.SetWallpaperAsync(path, cancellationToken).ConfigureAwait(false);

            var settings = _settings.Current;
            settings.LastSwitchTime = DateTime.Now;
            settings.LastWallpaperCategory = entry.Category;
            settings.LastWallpaperProvider = entry.ProviderName;
            settings.LastWallpaperSourceUrl = entry.SourceUrl;
            settings.LastWallpaperPath = path;
            AdvanceNextSwitchTime(settings);
            await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);

            _log.Write(
                WallpaperLogLevel.Info,
                $"已使用缓存壁纸（{entry.Category}）",
                entry.Category,
                entry.ProviderName);

            var result = new WallpaperSwitchResult(
                true,
                $"已切换到缓存壁纸（{entry.Category}）。{setResult.Message}",
                entry.Category,
                entry.ProviderName,
                entry.SourceUrl,
                path);

            SwitchCompleted?.Invoke(this, result);
            return result;
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public DateTime? ComputeNextSwitchTime(AppSettings settings, DateTime now)
    {
        var span = settings.Interval.ToTimeSpan();
        if (span is null || span.Value <= TimeSpan.Zero)
        {
            return null;
        }

        var anchor = settings.LastSwitchTime ?? now;
        var next = anchor + span.Value;

        // 设备关机 / 休眠期间错过的时间点一次性顺延到未来，避免启动后连续补切。
        var guard = 0;
        while (next <= now && guard++ < 1000)
        {
            next += span.Value;
        }

        return next;
    }

    private async Task<WallpaperSwitchResult> RunSwitchAsync(bool isAutomatic, CancellationToken cancellationToken)
    {
        var settings = _settings.Current;

        if (isAutomatic)
        {
            if (!settings.IsEnabled)
            {
                return new WallpaperSwitchResult(false, "自动切换已关闭，本次跳过。");
            }

            if (settings.Interval == SwitchInterval.Never)
            {
                return new WallpaperSwitchResult(false, "当前为“永久”模式，已跳过自动切换。");
            }
        }

        var selected = settings.SelectedCategories;
        if (selected.Count == 0)
        {
            var message = "还没有选择任何壁纸类别，请先在“类别设置”中至少选择 1 个类别。";
            _log.Write(WallpaperLogLevel.Warning, message);
            return new WallpaperSwitchResult(false, message);
        }

        var category = selected[Random.Shared.Next(selected.Count)];
        var keyword = await _categories.ResolveKeywordAsync(category, cancellationToken).ConfigureAwait(false);

        var targetWidth = _display.ScreenWidth;
        var targetHeight = _display.ScreenHeight;
        var cache = _cache as WallpaperCacheService;
        if (cache is not null)
        {
            cache.MaxEntries = Math.Max(1, settings.CacheSize);
        }

        var query = new WallpaperQuery(
            category,
            keyword,
            targetWidth,
            targetHeight,
            _cache.RecentKeys);

        // 依次尝试各个壁纸源，任一成功即结束；全部失败则保持当前壁纸（需求 3.3.3）。
        var failures = new List<string>();
        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!provider.CanHandle(query))
            {
                continue;
            }

            WallpaperCandidate? candidate;
            try
            {
                candidate = await provider.GetRandomAsync(query, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failures.Add($"{provider.Name}: {ex.Message}");
                continue;
            }

            if (candidate is null)
            {
                failures.Add($"{provider.Name}: 没有返回可用图片");
                continue;
            }

            var download = await _downloader.DownloadAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (!download.Success || download.Data is null)
            {
                failures.Add($"{provider.Name}: {download.Message}");
                continue;
            }

            var processed = await _imageProcessor
                .ProcessAsync(download.Data, targetWidth, targetHeight, cancellationToken)
                .ConfigureAwait(false);

            if (processed is null)
            {
                failures.Add($"{provider.Name}: 图片处理失败");
                continue;
            }

            return await ApplyAsync(candidate, processed, cancellationToken).ConfigureAwait(false);
        }

        // 需求：两个图源都取不到可用图片时，保持当前壁纸不变，并且不向用户提示警告或报错。
        // 因此这里只以 Info 级别留档（详细原因写入 detail 便于排查），不产生 Warning / Error 级日志。
        var reason = failures.Count == 0 ? "没有可用的壁纸源" : string.Join("；", failures);
        _logger.LogInformation("未获取到新壁纸，已保持当前壁纸不变：{Reason}", reason);

        _log.Write(
            WallpaperLogLevel.Info,
            $"本次未获取到新壁纸（{category}），已保持当前壁纸不变。",
            category,
            detail: reason);

        // 保持当前壁纸，仅把下一次切换时间顺延到未来的预设时间点。
        AdvanceNextSwitchTime(settings);
        await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);

        var failureResult = new WallpaperSwitchResult(false, "本次未能获取新壁纸，已保持当前壁纸不变。", category);
        SwitchCompleted?.Invoke(this, failureResult);
        return failureResult;
    }

    /// <summary>保存处理后的图片、设置壁纸并更新设置与日志。</summary>
    private async Task<WallpaperSwitchResult> ApplyAsync(
        WallpaperCandidate candidate,
        byte[] processed,
        CancellationToken cancellationToken)
    {
        // 先写入固定路径，保证 Windows 桌面壁纸来源文件长期存在。
        await File.WriteAllBytesAsync(CurrentWallpaperPath!, processed, cancellationToken).ConfigureAwait(false);

        var setResult = await _setter.SetWallpaperAsync(CurrentWallpaperPath!, cancellationToken).ConfigureAwait(false);

        // 缓存一份，供去重与断网兜底使用。
        await _cache.StoreAsync(processed, candidate, cancellationToken).ConfigureAwait(false);

        var settings = _settings.Current;
        settings.LastSwitchTime = DateTime.Now;
        settings.LastWallpaperCategory = candidate.Category;
        settings.LastWallpaperProvider = candidate.ProviderName;
        settings.LastWallpaperSourceUrl = candidate.Url;
        settings.LastWallpaperPath = CurrentWallpaperPath;
        settings.RecentImageKeys.Insert(0, candidate.Url);
        while (settings.RecentImageKeys.Count > RecentKeyLimit)
        {
            settings.RecentImageKeys.RemoveAt(settings.RecentImageKeys.Count - 1);
        }

        AdvanceNextSwitchTime(settings);
        await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);

        var message = $"壁纸已更新：{candidate.Category} · {candidate.ProviderName} · {setResult.Message}";
        _log.Write(WallpaperLogLevel.Info, message, candidate.Category, candidate.ProviderName);
        _logger.LogInformation("壁纸切换成功：{Category} / {Provider}", candidate.Category, candidate.ProviderName);

        var result = new WallpaperSwitchResult(
            true,
            message,
            candidate.Category,
            candidate.ProviderName,
            candidate.Url,
            CurrentWallpaperPath);

        SwitchCompleted?.Invoke(this, result);
        return result;
    }

    /// <summary>把下一次切换时间顺延到未来（“永久”模式保持为 <c>null</c>）。</summary>
    private void AdvanceNextSwitchTime(AppSettings settings)
    {
        settings.NextSwitchTime = ComputeNextSwitchTime(settings, DateTime.Now);
    }
}
