using System.Text.Json;
using DailyWallpaper.Models;
using Microsoft.Maui.Storage;

namespace DailyWallpaper.Services;

/// <summary>
/// 基于本地 JSON 文件的设置存储实现（无账号、无云同步）。
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ILogService _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SettingsService(ILogService log)
    {
        _log = log;
        SettingsFilePath = Path.Combine(FileSystem.AppDataDirectory, "settings.json");
    }

    /// <inheritdoc />
    public AppSettings Current { get; private set; } = new();

    /// <inheritdoc />
    public string SettingsFilePath { get; }

    /// <inheritdoc />
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                Current = new AppSettings();
                return Current;
            }

            await using var stream = File.OpenRead(SettingsFilePath);
            var loaded = await JsonSerializer
                .DeserializeAsync(stream, AppJsonContext.Default.AppSettings, cancellationToken)
                .ConfigureAwait(false);

            Current = loaded ?? new AppSettings();
            Normalize(Current);
        }
        catch (Exception ex)
        {
            // 配置文件损坏时不能崩溃：回退到默认设置并记录原因。
            _log.Write(WallpaperLogLevel.Warning, "读取 settings.json 失败，已回退到默认设置", detail: ex.ToString());
            Current = new AppSettings();
        }
        finally
        {
            _gate.Release();
        }

        return Current;
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken = default) => SaveAsync(Current, cancellationToken);

    /// <inheritdoc />
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Normalize(settings);
            Current = settings;

            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 先写临时文件再替换，避免写入过程中被系统杀死导致配置文件损坏。
            var tempPath = SettingsFilePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, settings, AppJsonContext.Default.AppSettings, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempPath, SettingsFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.Write(WallpaperLogLevel.Error, "保存 settings.json 失败", detail: ex.ToString());
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>修正非法或缺失的字段，保证后续逻辑拿到的数据始终合法。</summary>
    private static void Normalize(AppSettings settings)
    {
        settings.CustomCategories ??= new List<string>();
        settings.SelectedCategories ??= new List<string>();
        settings.RecentImageKeys ??= new List<string>();

        settings.CustomCategories = settings.CustomCategories
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Take(Defaults.MaxCustomCategories)
            .ToList();

        settings.SelectedCategories = settings.SelectedCategories
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Take(Defaults.MaxSelectedCategories)
            .ToList();

        if (settings.CacheSize is < 1 or > 30)
        {
            settings.CacheSize = Defaults.DefaultCacheSize;
        }
    }
}
