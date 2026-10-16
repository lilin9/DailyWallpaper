using System.Net;
using DailyWallpaper.Models;
using Microsoft.Extensions.Logging;

namespace DailyWallpaper.Services;

/// <summary>
/// 基于 <see cref="IHttpClientFactory"/> 的图片下载实现，带重试与体积限制。
/// </summary>
public sealed class WallpaperDownloader : IWallpaperDownloader
{
    /// <summary>单张图片的最大允许体积（20 MB），防止异常源拖垮设备。</summary>
    private const long MaxImageBytes = 20L * 1024 * 1024;

    private const int MaxAttempts = 3;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WallpaperDownloader> _logger;

    public WallpaperDownloader(IHttpClientFactory httpClientFactory, ILogger<WallpaperDownloader> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WallpaperDownloadResult> DownloadAsync(
        WallpaperCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var client = _httpClientFactory.CreateClient(WallpaperHttpClients.Download);
                using var response = await client
                    .GetAsync(candidate.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("下载 {Url} 返回状态码 {StatusCode}", candidate.Url, (int)response.StatusCode);
                    lastError = new HttpRequestException($"HTTP {(int)response.StatusCode}");

                    // 4xx 属于确定性错误（地址失效等），直接换下一张，不重试。
                    if (response.StatusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError)
                    {
                        return WallpaperDownloadResult.Fail($"下载失败：HTTP {(int)response.StatusCode}");
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var declaredLength = response.Content.Headers.ContentLength;
                if (declaredLength is > MaxImageBytes)
                {
                    return WallpaperDownloadResult.Fail("图片体积超过上限，已跳过");
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                if (bytes.Length == 0)
                {
                    return WallpaperDownloadResult.Fail("下载到的内容为空");
                }

                if (bytes.Length > MaxImageBytes)
                {
                    return WallpaperDownloadResult.Fail("下载到的图片体积超过上限，已跳过");
                }

                return WallpaperDownloadResult.Ok(bytes);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                lastError = ex;
                _logger.LogWarning(ex, "第 {Attempt} 次下载 {Url} 失败", attempt, candidate.Url);

                if (attempt < MaxAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return WallpaperDownloadResult.Fail(lastError?.Message ?? "网络请求失败");
    }
}
