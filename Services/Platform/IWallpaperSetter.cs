using DailyWallpaper.Models;

namespace DailyWallpaper.Services.Platform;

/// <summary>
/// 平台壁纸设置能力：Android 使用 <c>WallpaperManager</c>，Windows 使用 Win32 API。
/// </summary>
public interface IWallpaperSetter
{
    /// <summary>平台名称，用于日志与界面展示。</summary>
    string PlatformName { get; }

    /// <summary>当前平台是否支持设置锁屏壁纸。</summary>
    bool SupportsLockScreen { get; }

    /// <summary>
    /// 把本地图片设为壁纸。失败时抛出异常，由上层转换为用户提示。
    /// </summary>
    Task<WallpaperSetResult> SetWallpaperAsync(string filePath, CancellationToken cancellationToken = default);
}
