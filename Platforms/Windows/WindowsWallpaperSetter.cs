using DailyWallpaper.Models;
using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;
using Microsoft.Extensions.Logging;

namespace DailyWallpaper.Platforms.Windows;

/// <summary>
/// Windows 桌面壁纸设置：调用 Win32 <c>SystemParametersInfo(SPI_SETDESKWALLPAPER)</c>，
/// 并在系统允许时尝试设置锁屏壁纸（WinRT <c>LockScreen.SetImageFileAsync</c>），失败则降级并提示。
/// </summary>
public sealed class WindowsWallpaperSetter : IWallpaperSetter
{
    private readonly ILogService _log;
    private readonly ILogger<WindowsWallpaperSetter> _logger;

    /// <summary>锁屏设置在系统不接受时会永久降级，避免每次切换都重复失败。</summary>
    private bool _lockScreenAvailable = true;

    public WindowsWallpaperSetter(ILogService log, ILogger<WindowsWallpaperSetter> logger)
    {
        _log = log;
        _logger = logger;
    }

    /// <inheritdoc />
    public string PlatformName => "Windows 11";

    /// <inheritdoc />
    public bool SupportsLockScreen => _lockScreenAvailable;

    /// <inheritdoc />
    public async Task<WallpaperSetResult> SetWallpaperAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("壁纸文件不存在", filePath);
        }

        if (!WindowsNativeMethods.SystemParametersInfo(
                WindowsNativeMethods.SPI_SETDESKWALLPAPER,
                0,
                filePath,
                WindowsNativeMethods.SPIF_UPDATEINIFILE | WindowsNativeMethods.SPIF_SENDWININICHANGE))
        {
            var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"SystemParametersInfo 设置桌面壁纸失败（Win32 错误码 {error}）。");
        }

        _logger.LogInformation("桌面壁纸已设置为 {Path}", filePath);

        var lockScreenApplied = false;
        var message = "桌面壁纸已更新";

        if (_lockScreenAvailable)
        {
            try
            {
                lockScreenApplied = await TrySetLockScreenAsync(filePath, cancellationToken).ConfigureAwait(false);
                message = lockScreenApplied
                    ? "桌面与锁屏壁纸已更新"
                    : "桌面壁纸已更新（锁屏壁纸未修改）";
            }
            catch (Exception ex)
            {
                // 需求 3.2：系统 API 受限时降级，只设置桌面壁纸并提示用户。
                _lockScreenAvailable = false;
                message = "桌面壁纸已更新；Windows 未允许修改锁屏壁纸，已自动降级";
                _log.Write(WallpaperLogLevel.Warning, "Windows 锁屏壁纸设置失败，已降级为仅设置桌面壁纸", ex);
            }
        }
        else
        {
            message = "桌面壁纸已更新（锁屏壁纸不支持，已降级）";
        }

        return new WallpaperSetResult(true, lockScreenApplied, message);
    }

    /// <summary>
    /// 尝试通过 WinRT API 设置锁屏壁纸。部分系统（组策略限制、非打包应用）会拒绝该操作。
    /// </summary>
    private static async Task<bool> TrySetLockScreenAsync(string filePath, CancellationToken cancellationToken)
    {
        var file = await global::Windows.Storage.StorageFile
            .GetFileFromPathAsync(filePath)
            .AsTask(cancellationToken)
            .ConfigureAwait(false);

        // SetImageFileAsync 返回 IAsyncAction（无返回值），成功即视为已设置。
        await global::Windows.System.UserProfile.LockScreen
            .SetImageFileAsync(file)
            .AsTask(cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
