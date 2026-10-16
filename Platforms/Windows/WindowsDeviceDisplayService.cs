using DailyWallpaper.Services.Platform;

namespace DailyWallpaper.Platforms.Windows;

/// <summary>
/// Windows 屏幕信息：使用 Win32 <c>GetSystemMetrics</c> 读取主屏物理分辨率，
/// 壁纸会按该分辨率等比例缩放并居中裁剪（需求 3.2）。
/// </summary>
public sealed class WindowsDeviceDisplayService : IDeviceDisplayService
{
    /// <inheritdoc />
    public int ScreenWidth
    {
        get
        {
            var width = WindowsNativeMethods.GetSystemMetrics(WindowsNativeMethods.SM_CXSCREEN);
            return width > 0 ? width : 1920;
        }
    }

    /// <inheritdoc />
    public int ScreenHeight
    {
        get
        {
            var height = WindowsNativeMethods.GetSystemMetrics(WindowsNativeMethods.SM_CYSCREEN);
            return height > 0 ? height : 1080;
        }
    }

    /// <inheritdoc />
    public string DeviceDescription => $"Windows 11 · 主屏 {ScreenWidth}×{ScreenHeight}";
}
