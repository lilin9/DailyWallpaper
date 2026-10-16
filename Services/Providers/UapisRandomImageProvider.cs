using DailyWallpaper.Models;
using Microsoft.Extensions.Logging;

namespace DailyWallpaper.Services.Providers;

/// <summary>
/// Uapi 随机图片接口（<c>https://uapis.cn/api/v1/random/image</c>），完全免费、无需注册。
/// </summary>
/// <remarks>
/// <para>接口文档：<c>https://b1.uapis.cn/docs/api-reference/get-random-image</c>。</para>
/// <para>
/// 请求形式：<c>/api/v1/random/image?category=anime</c>。<c>category</c> 为可选项，
/// 支持 <c>anime</c>（混合动漫）、<c>acg</c>（二次元）、<c>landscape</c>（风景）等主类别；
/// 不传 <c>category</c> 时会在全部图片来源中随机抽取。
/// </para>
/// <para>
/// 需要注意：官方文档列出的一些类别（如 <c>aidrawing</c>、<c>pcwallpaper</c>）实际可能返回 <c>404</c>，
/// 因此这里在指定类别请求失败时会自动退化为“全局随机”，不会因为类别下线而取不到图片。
/// </para>
/// <para>响应为 <c>302</c>，其 <c>Location</c> 指向真实图片地址（部分类别会再跳转到第三方图床）。</para>
/// </remarks>
public sealed class UapisRandomImageProvider : IWallpaperProvider
{
    private const string BaseUrl = "https://uapis.cn/api/v1/random/image";

    /// <summary>每个地址最多请求几次（用于换一张最近没用过的图片）。</summary>
    /// <remarks>该接口对访客有每秒 4 次的限流，因此重试次数保持得比较保守。</remarks>
    private const int MaxAttemptsPerUrl = 2;

    /// <summary>
    /// 关键词 → 接口类别。按表内顺序匹配，先命中者胜出；未命中的关键词不传 <c>category</c>（全局随机）。
    /// </summary>
    private static readonly (string Pattern, string Category)[] CategoryMap =
    [
        // 动漫 / 二次元系
        ("anime", "anime"),
        ("动漫", "anime"),
        ("二次元", "anime"),
        ("acg", "acg"),
        ("插画", "acg"),
        ("illustration", "acg"),
        ("cyberpunk", "acg"),
        ("赛博朋克", "acg"),

        // AI 绘画（文档中为 aidrawing，实际可能已下线，届时自动退化为全局随机）
        ("ai", "aidrawing"),
        ("aidrawing", "aidrawing"),
        ("ai绘画", "aidrawing"),
        ("art", "aidrawing"),

        // 风景
        ("landscape", "landscape"),
        ("nature", "landscape"),
        ("风景", "landscape"),

        // 其它可用类别
        ("furry", "furry"),
        ("表情包", "bq"),
        ("bq", "bq")
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UapisRandomImageProvider> _logger;

    public UapisRandomImageProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<UapisRandomImageProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Uapi随机图";

    /// <inheritdoc />
    public bool CanHandle(WallpaperQuery query) => true;

    /// <inheritdoc />
    public async Task<WallpaperCandidate?> GetRandomAsync(
        WallpaperQuery query,
        CancellationToken cancellationToken = default)
    {
        var category = KeywordCategoryMatcher.Match(query.Keyword, CategoryMap);

        // 优先按类别取图，类别不存在（404）时退化为全局随机。
        var requestUrls = new List<string>();
        if (category is not null)
        {
            requestUrls.Add($"{BaseUrl}?category={Uri.EscapeDataString(category)}");
        }

        requestUrls.Add(BaseUrl);

        try
        {
            using var client = _httpClientFactory.CreateClient(WallpaperHttpClients.ApiQuery);
            var url = await ImageSourceResolver
                .ResolveRandomImageUrlAsync(client, requestUrls, query.RecentKeys, MaxAttemptsPerUrl, cancellationToken)
                .ConfigureAwait(false);

            if (url is null)
            {
                _logger.LogWarning("Uapi 未返回可用图片：{Keyword}", query.Keyword);
                return null;
            }

            _logger.LogDebug("Uapi 返回壁纸（{Category}）：{Url}", category ?? "全局随机", url);
            return new WallpaperCandidate(url, Name, query.Category, query.Keyword, 0, 0);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogWarning(ex, "Uapi 请求失败：{Keyword}", query.Keyword);
            return null;
        }
    }
}
