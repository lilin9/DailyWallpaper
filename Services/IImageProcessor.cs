using DailyWallpaper.Models;

namespace DailyWallpaper.Services;

/// <summary>
/// 壁纸图片处理：等比例缩放到覆盖目标屏幕后居中裁剪（需求 3.2），使用 SkiaSharp 实现。
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// 处理图片。<paramref name="source"/> 为原始图片字节，返回处理后的 JPEG 字节；
    /// 解码失败时返回 <c>null</c>。
    /// </summary>
    Task<byte[]?> ProcessAsync(
        byte[] source,
        int targetWidth,
        int targetHeight,
        CancellationToken cancellationToken = default);

    /// <summary>读取图片的像素尺寸；失败返回 <c>(0, 0)</c>。</summary>
    (int Width, int Height) GetImageSize(byte[] source);
}
