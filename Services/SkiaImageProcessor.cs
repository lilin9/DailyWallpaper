using DailyWallpaper.Models;
using SkiaSharp;

namespace DailyWallpaper.Services;

/// <summary>
/// SkiaSharp 实现：先按 <c>max(目标宽/源宽, 目标高/源高)</c> 缩放至覆盖目标区域，再居中裁剪，避免拉伸变形。
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor
{
    /// <summary>输出 JPEG 质量，兼顾体积与肉眼观感。</summary>
    private const int OutputQuality = 92;

    private readonly ILogService _log;

    public SkiaImageProcessor(ILogService log)
    {
        _log = log;
    }

    /// <inheritdoc />
    public Task<byte[]?> ProcessAsync(
        byte[] source,
        int targetWidth,
        int targetHeight,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Process(source, targetWidth, targetHeight, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public (int Width, int Height) GetImageSize(byte[] source)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(source);
            return bitmap is null ? (0, 0) : (bitmap.Width, bitmap.Height);
        }
        catch (Exception ex)
        {
            _log.Write(WallpaperLogLevel.Warning, "读取图片尺寸失败", ex);
            return (0, 0);
        }
    }

    private byte[]? Process(byte[] source, int targetWidth, int targetHeight, CancellationToken cancellationToken)
    {
        // 取消（例如应用退出）时立即返回，不产生半成品缓存。
        cancellationToken.ThrowIfCancellationRequested();

        using var original = SKBitmap.Decode(source);
        if (original is null)
        {
            _log.Write(WallpaperLogLevel.Warning, "图片解码失败，可能不是受支持的图片格式");
            return null;
        }

        var width = targetWidth > 0 ? targetWidth : original.Width;
        var height = targetHeight > 0 ? targetHeight : original.Height;

        // 等比例放大到足以覆盖整个目标屏幕（需求：不考虑等比例修改带来的质量损失）。
        var scale = Math.Max((float)width / original.Width, (float)height / original.Height);
        var scaledWidth = Math.Max(1, (int)Math.Ceiling(original.Width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Ceiling(original.Height * scale));

        using var scaled = original.Resize(
            new SKImageInfo(scaledWidth, scaledHeight, original.ColorType, original.AlphaType),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        if (scaled is null)
        {
            _log.Write(WallpaperLogLevel.Warning, "图片缩放失败");
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 居中裁剪：保留中间区域，保证画面主体居中且不变形。
        var left = Math.Max(0, (scaledWidth - width) / 2);
        var top = Math.Max(0, (scaledHeight - height) / 2);
        var cropWidth = Math.Min(width, scaledWidth);
        var cropHeight = Math.Min(height, scaledHeight);

        using var surface = SKSurface.Create(new SKImageInfo(cropWidth, cropHeight, SKColorType.Rgb888x, SKAlphaType.Opaque));
        if (surface is null)
        {
            _log.Write(WallpaperLogLevel.Warning, "创建绘图画布失败");
            return null;
        }

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);
        canvas.DrawBitmap(
            scaled,
            new SKRect(left, top, left + cropWidth, top + cropHeight),
            new SKRect(0, 0, cropWidth, cropHeight),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        canvas.Flush();

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, OutputQuality);
        return encoded?.ToArray();
    }
}
