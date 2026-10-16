using DailyWallpaper.Services.Platform;

namespace DailyWallpaper.Platforms.Android;

/// <summary>
/// Android 屏幕信息：使用 <c>DisplayMetrics</c> 的真实像素尺寸，作为壁纸缩放裁剪的目标分辨率。
/// </summary>
public sealed class AndroidDeviceDisplayService : IDeviceDisplayService
{
    /// <inheritdoc />
    public int ScreenWidth
    {
        get
        {
            var metrics = global::Android.App.Application.Context.Resources?.DisplayMetrics;
            return metrics is { WidthPixels: > 0 } ? metrics.WidthPixels : 1080;
        }
    }

    /// <inheritdoc />
    public int ScreenHeight
    {
        get
        {
            var metrics = global::Android.App.Application.Context.Resources?.DisplayMetrics;
            return metrics is { HeightPixels: > 0 } ? metrics.HeightPixels : 2400;
        }
    }

    /// <inheritdoc />
    public string DeviceDescription
    {
        get
        {
            var version = global::Android.OS.Build.VERSION.Release;
            var manufacturer = global::Android.OS.Build.Manufacturer;
            var model = global::Android.OS.Build.Model;
            return $"Android {version} · {manufacturer} {model} · {ScreenWidth}×{ScreenHeight}";
        }
    }
}
