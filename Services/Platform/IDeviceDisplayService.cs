namespace DailyWallpaper.Services.Platform;

/// <summary>
/// 设备屏幕信息：用于把壁纸等比例缩放裁剪到目标分辨率（需求 3.2）。
/// </summary>
public interface IDeviceDisplayService
{
    /// <summary>屏幕像素宽度。</summary>
    int ScreenWidth { get; }

    /// <summary>屏幕像素高度。</summary>
    int ScreenHeight { get; }

    /// <summary>设备 / 平台描述，用于界面展示。</summary>
    string DeviceDescription { get; }
}
