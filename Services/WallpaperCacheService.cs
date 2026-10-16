using System.Text.Json;
using DailyWallpaper.Models;
using Microsoft.Maui.Storage;

namespace DailyWallpaper.Services;

/// <summary>
/// 基于本地目录 + 索引文件的壁纸缓存实现。
/// </summary>
public sealed class WallpaperCacheService : IWallpaperCache
{
    private const string IndexFileName = "index.json";

    private readonly ILogService _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _indexFilePath;

    private List<WallpaperCacheEntry> _entries = new();

    public WallpaperCacheService(ILogService log)
    {
        _log = log;
        CacheDirectory = Path.Combine(FileSystem.AppDataDirectory, "wallpapers");
        Directory.CreateDirectory(CacheDirectory);
        _indexFilePath = Path.Combine(CacheDirectory, IndexFileName);
    }

    /// <inheritdoc />
    public string CacheDirectory { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> RecentKeys
    {
        get
        {
            lock (_entries)
            {
                return _entries.Select(entry => entry.Key).ToList();
            }
        }
    }

    /// <summary>缓存条数上限，由设置决定（默认 5）。</summary>
    public int MaxEntries { get; set; } = Defaults.DefaultCacheSize;

    /// <inheritdoc />
    public async Task<string> StoreAsync(
        byte[] processedImage,
        WallpaperCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        var key = string.IsNullOrWhiteSpace(candidate.Url) ? Guid.NewGuid().ToString("N") : candidate.Url;
        var fileName = $"{Math.Abs(StableHash(key)):x8}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
        var fullPath = Path.Combine(CacheDirectory, fileName);

        await File.WriteAllBytesAsync(fullPath, processedImage, cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _entries.RemoveAll(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
            _entries.Add(new WallpaperCacheEntry
            {
                FileName = fileName,
                Key = key,
                Category = candidate.Category,
                ProviderName = candidate.ProviderName,
                SourceUrl = candidate.Url,
                CreatedTime = DateTime.Now
            });

            await SaveIndexAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Write(WallpaperLogLevel.Warning, "写入壁纸缓存索引失败", ex);
        }
        finally
        {
            _gate.Release();
        }

        await CleanupAsync(cancellationToken).ConfigureAwait(false);
        return fullPath;
    }

    /// <inheritdoc />
    public async Task<WallpaperCacheEntry?> GetRandomAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        List<WallpaperCacheEntry> snapshot;
        lock (_entries)
        {
            snapshot = _entries.Where(entry => File.Exists(GetPath(entry.FileName))).ToList();
        }

        if (snapshot.Count == 0)
        {
            return null;
        }

        return snapshot[Random.Shared.Next(snapshot.Count)];
    }

    /// <inheritdoc />
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _entries.Sort((left, right) => right.CreatedTime.CompareTo(left.CreatedTime));

            var limit = Math.Max(1, MaxEntries);
            while (_entries.Count > limit)
            {
                var oldest = _entries[^1];
                _entries.RemoveAt(_entries.Count - 1);

                try
                {
                    var path = GetPath(oldest.FileName);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (IOException)
                {
                    // 文件被系统占用时忽略，下次清理再试。
                }
            }

            await SaveIndexAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_entries)
            {
                _entries.Clear();
            }

            foreach (var file in Directory.EnumerateFiles(CacheDirectory, "*.jpg"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // 忽略占用中的文件。
                }
            }

            await SaveIndexAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetPath(string fileName) => Path.Combine(CacheDirectory, fileName);

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_entries.Count > 0 || !File.Exists(_indexFilePath))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_entries.Count > 0)
            {
                return;
            }

            await using var stream = File.OpenRead(_indexFilePath);
            var index = await JsonSerializer
                .DeserializeAsync(stream, AppJsonContext.Default.WallpaperCacheIndex, cancellationToken)
                .ConfigureAwait(false);

            _entries = index?.Entries ?? new List<WallpaperCacheEntry>();
        }
        catch (Exception ex)
        {
            _log.Write(WallpaperLogLevel.Warning, "读取壁纸缓存索引失败", ex);
            _entries = new List<WallpaperCacheEntry>();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveIndexAsync(CancellationToken cancellationToken)
    {
        var index = new WallpaperCacheIndex();
        lock (_entries)
        {
            index.Entries = _entries.ToList();
        }

        var tempPath = _indexFilePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, index, AppJsonContext.Default.WallpaperCacheIndex, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, _indexFilePath, overwrite: true);
    }

    /// <summary>稳定的字符串哈希，用于生成可复现的文件名前缀。</summary>
    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
            {
                hash = (hash * 31) + character;
            }

            return hash;
        }
    }
}
