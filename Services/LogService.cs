using System.Text;
using System.Text.Json;
using DailyWallpaper.Models;
using Microsoft.Maui.Storage;

namespace DailyWallpaper.Services;

/// <summary>
/// <see cref="ILogService"/> 的默认实现：JSON Lines 文件 + 内存缓存。
/// </summary>
public sealed class LogService : ILogService
{
    /// <summary>内存中保留的最大条数，避免长期运行占用过多内存。</summary>
    private const int MaxEntries = 300;

    /// <summary>日志文件超过该大小（字节）时压缩重写。</summary>
    private const long MaxFileBytes = 512 * 1024;

    private readonly object _sync = new();
    private readonly List<WallpaperLogEntry> _entries = new();

    public LogService()
    {
        var directory = Path.Combine(FileSystem.AppDataDirectory, "logs");
        Directory.CreateDirectory(directory);
        LogFilePath = Path.Combine(directory, "wallpaper.jsonl");
    }

    /// <inheritdoc />
    public event EventHandler<WallpaperLogEntry>? EntryAdded;

    /// <inheritdoc />
    public IReadOnlyList<WallpaperLogEntry> Entries
    {
        get
        {
            lock (_sync)
            {
                return _entries.ToList();
            }
        }
    }

    /// <inheritdoc />
    public string LogFilePath { get; }

    /// <inheritdoc />
    public void Write(
        WallpaperLogLevel level,
        string message,
        string? category = null,
        string? source = null,
        string? detail = null)
    {
        var entry = new WallpaperLogEntry
        {
            Time = DateTime.Now,
            Level = level,
            Message = message,
            Category = category ?? string.Empty,
            Source = source ?? string.Empty,
            Detail = detail
        };

        Append(entry);
    }

    /// <inheritdoc />
    public void Write(
        WallpaperLogLevel level,
        string message,
        Exception exception,
        string? category = null,
        string? source = null) =>
        Write(level, message, category, source, exception.ToString());

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(LogFilePath))
            {
                return;
            }

            var lines = await File.ReadAllLinesAsync(LogFilePath, cancellationToken).ConfigureAwait(false);
            var parsed = new List<WallpaperLogEntry>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize(line, AppJsonContext.Default.WallpaperLogEntry);
                    if (entry is not null)
                    {
                        parsed.Add(entry);
                    }
                }
                catch (JsonException)
                {
                    // 单行损坏不影响其它日志。
                }
            }

            parsed.Reverse(); // 文件是按时间正序追加的，界面需要新的在前。

            lock (_sync)
            {
                _entries.Clear();
                _entries.AddRange(parsed.Take(MaxEntries));
            }
        }
        catch (Exception ex)
        {
            Write(WallpaperLogLevel.Warning, "读取日志文件失败", ex);
        }
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _entries.Clear();
        }

        try
        {
            if (File.Exists(LogFilePath))
            {
                File.Delete(LogFilePath);
            }
        }
        catch (Exception ex)
        {
            Write(WallpaperLogLevel.Warning, "删除日志文件失败", ex);
        }

        return Task.CompletedTask;
    }

    private void Append(WallpaperLogEntry entry)
    {
        try
        {
            lock (_sync)
            {
                _entries.Insert(0, entry);
                if (_entries.Count > MaxEntries)
                {
                    _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
                }

                var line = JsonSerializer.Serialize(entry, AppJsonContext.Default.WallpaperLogEntry);
                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);

                if (new FileInfo(LogFilePath).Length > MaxFileBytes)
                {
                    TrimFile();
                }
            }
        }
        catch
        {
            // 日志功能本身不允许影响主流程。
        }

        EntryAdded?.Invoke(this, entry);
    }

    /// <summary>日志文件过大时，仅保留最近 <see cref="MaxEntries"/> 条。</summary>
    private void TrimFile()
    {
        var lines = new List<string>();
        foreach (var item in _entries)
        {
            lines.Add(JsonSerializer.Serialize(item, AppJsonContext.Default.WallpaperLogEntry));
        }

        lines.Reverse();
        File.WriteAllLines(LogFilePath, lines, Encoding.UTF8);
    }
}
