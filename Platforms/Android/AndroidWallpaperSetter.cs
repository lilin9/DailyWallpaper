using Android.App;
using Android.Graphics;
using DailyWallpaper.Models;
using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;
using Microsoft.Extensions.Logging;

namespace DailyWallpaper.Platforms.Android;

/// <summary>
/// Android 壁纸设置（需求 3.2）：通过 <c>WallpaperManager</c> 同时设置桌面与锁屏壁纸。
/// </summary>
public sealed class AndroidWallpaperSetter : IWallpaperSetter
{
    private readonly ILogService _log;
    private readonly ILogger<AndroidWallpaperSetter> _logger;

    public AndroidWallpaperSetter(ILogService log, ILogger<AndroidWallpaperSetter> logger)
    {
        _log = log;
        _logger = logger;
    }

    /// <inheritdoc />
    public string PlatformName => "Android";

    /// <inheritdoc />
    public bool SupportsLockScreen => true;

    /// <inheritdoc />
    public Task<WallpaperSetResult> SetWallpaperAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.Run(() => SetWallpaper(filePath), cancellationToken);

    private WallpaperSetResult SetWallpaper(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("壁纸文件不存在", filePath);
        }

        var context = global::Android.App.Application.Context;
        var manager = WallpaperManager.GetInstance(context)
                      ?? throw new InvalidOperationException("无法获取 WallpaperManager。");

        using var bitmap = BitmapFactory.DecodeFile(filePath)
                           ?? throw new InvalidOperationException("壁纸图片解码失败。");

        // 桌面壁纸：FLAG_SYSTEM；锁屏壁纸：FLAG_LOCK（需求 3.2）。
        try
        {
            manager.SetBitmap(bitmap, null, true, WallpaperManagerFlags.System | WallpaperManagerFlags.Lock);
            _logger.LogInformation("已同时设置桌面与锁屏壁纸：{Path}", filePath);
            return new WallpaperSetResult(true, true, "桌面与锁屏壁纸已更新");
        }
        catch (Exception ex)
        {
            // 个别系统 / 定制 ROM 可能拒绝设置锁屏，此时降级为只设置桌面壁纸。
            _log.Write(WallpaperLogLevel.Warning, "设置锁屏壁纸失败，尝试仅设置桌面壁纸", ex);

            manager.SetBitmap(bitmap, null, true, WallpaperManagerFlags.System);
            return new WallpaperSetResult(true, false, "桌面壁纸已更新（锁屏设置被系统拒绝，已降级）");
        }
    }
}
