namespace DailyWallpaper.Models;

/// <summary>
/// 一次壁纸获取请求的参数。
/// </summary>
/// <param name="Category">用户选中的类别名称。</param>
/// <param name="Keyword">类别映射到的搜索关键词。</param>
/// <param name="TargetWidth">目标屏幕宽度（像素）。</param>
/// <param name="TargetHeight">目标屏幕高度（像素）。</param>
/// <param name="RecentKeys">最近使用过的图片标识，用于避免连续切换使用同一张图片。</param>
public sealed record WallpaperQuery(
    string Category,
    string Keyword,
    int TargetWidth,
    int TargetHeight,
    IReadOnlyCollection<string> RecentKeys);

/// <summary>
/// 壁纸源返回的候选图片信息（尚未下载）。
/// </summary>
/// <param name="Url">图片原始地址。</param>
/// <param name="ProviderName">壁纸源名称，例如 Ospyo壁纸。</param>
/// <param name="Category">类别名称。</param>
/// <param name="Keyword">使用的搜索关键词。</param>
/// <param name="Width">图片宽度（未知时为 0）。</param>
/// <param name="Height">图片高度（未知时为 0）。</param>
public sealed record WallpaperCandidate(
    string Url,
    string ProviderName,
    string Category,
    string Keyword,
    int Width,
    int Height);

/// <summary>设置壁纸的结果。</summary>
/// <param name="DesktopApplied">桌面壁纸是否设置成功。</param>
/// <param name="LockScreenApplied">锁屏壁纸是否设置成功。</param>
/// <param name="Message">附加说明，例如降级原因。</param>
public sealed record WallpaperSetResult(bool DesktopApplied, bool LockScreenApplied, string Message)
{
    /// <summary>构造一个成功结果。</summary>
    public static WallpaperSetResult Success(bool lockScreen) =>
        new(true, lockScreen, lockScreen ? "桌面与锁屏壁纸已更新" : "桌面壁纸已更新（未设置锁屏）");
}

/// <summary>一次自动 / 手动切换的整体结果。</summary>
/// <param name="Success">是否成功更换了壁纸。</param>
/// <param name="Message">给用户看的提示文本。</param>
/// <param name="Category">本次使用的类别。</param>
/// <param name="ProviderName">本次使用的壁纸源。</param>
/// <param name="SourceUrl">本次图片地址。</param>
/// <param name="LocalPath">处理后的本地图片路径。</param>
public sealed record WallpaperSwitchResult(
    bool Success,
    string Message,
    string? Category = null,
    string? ProviderName = null,
    string? SourceUrl = null,
    string? LocalPath = null);
