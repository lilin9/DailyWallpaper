using DailyWallpaper.Models;

namespace DailyWallpaper.Services;

/// <summary>图片下载结果。</summary>
/// <param name="Success">是否下载成功。</param>
/// <param name="Data">图片字节内容。</param>
/// <param name="Message">失败原因（成功时为成功说明）。</param>
public sealed record WallpaperDownloadResult(bool Success, byte[]? Data, string Message)
{
    public static WallpaperDownloadResult Ok(byte[] data) => new(true, data, "下载成功");

    public static WallpaperDownloadResult Fail(string message) => new(false, null, message);
}

/// <summary>
/// 下载候选壁纸的原始图片（需求 3.2 / 3.3.3）。
/// </summary>
public interface IWallpaperDownloader
{
    /// <summary>
    /// 下载图片。失败时不抛异常，返回带原因的失败结果，调用方据此跳过本次切换并保持当前壁纸。
    /// </summary>
    Task<WallpaperDownloadResult> DownloadAsync(
        WallpaperCandidate candidate,
        CancellationToken cancellationToken = default);
}
