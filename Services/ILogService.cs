using DailyWallpaper.Models;

namespace DailyWallpaper.Services;

/// <summary>
/// 本地日志：内存中保留最近若干条供界面展示，同时追加写入 JSON Lines 文件。
/// </summary>
public interface ILogService
{
    /// <summary>新增一条日志时触发（可能在后台线程触发，订阅方需自行切回 UI 线程）。</summary>
    event EventHandler<WallpaperLogEntry>? EntryAdded;

    /// <summary>当前内存中的日志（新的在前）。</summary>
    IReadOnlyList<WallpaperLogEntry> Entries { get; }

    /// <summary>日志文件路径。</summary>
    string LogFilePath { get; }

    /// <summary>写入一条日志，内部已捕获所有异常，绝不抛出。</summary>
    void Write(
        WallpaperLogLevel level,
        string message,
        string? category = null,
        string? source = null,
        string? detail = null);

    /// <summary>记录异常。</summary>
    void Write(
        WallpaperLogLevel level,
        string message,
        Exception exception,
        string? category = null,
        string? source = null);

    /// <summary>从磁盘加载历史日志。</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>清空内存与磁盘日志。</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
