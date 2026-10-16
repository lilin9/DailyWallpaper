using DailyWallpaper.Models;
using Microsoft.Extensions.Logging;

namespace DailyWallpaper.Services.Providers;

/// <summary>
/// Ospyo 随机壁纸接口（<c>https://wallpaper.ospyo.com/api/v2</c>）。
/// </summary>
/// <remarks>
/// <para>接口文档：<c>https://wallpaper.ospyo.com/default/V2</c>。</para>
/// <para>
/// 请求形式：<c>/api/v2</c> 表示全局随机，<c>/api/v2/{type}</c> 表示指定类型。
/// 官方给出的类型为 <c>anime</c>（动漫）、<c>game</c>（游戏）、<c>nature</c>（自然风景）；
/// 无法识别的类型名会被服务端忽略并同样返回随机图片，因此本提供程序对无法映射的关键词直接使用全局随机。
/// </para>
/// <para>响应为 <c>302</c>，其 <c>Location</c> 指向真实图片地址（自动跟随重定向后即为最终图片地址）。</para>
/// <para>该接口只提供动漫 / 游戏 / 自然三个大类，无法按分辨率筛选，因此候选图片的宽高保持未知（0）。</para>
/// </remarks>
public sealed class OspyoWallpaperProvider : IWallpaperProvider
{
    /// <summary>全局随机地址。</summary>
    private const string BaseUrl = "https://wallpaper.ospyo.com/api/v2";

    /// <summary>每个地址最多请求几次（用于换一张最近没用过的图片）。</summary>
    private const int MaxAttemptsPerUrl = 3;

    /// <summary>
    /// 关键词 → 接口类型。按表内顺序匹配，先命中者胜出；
    /// 未命中的关键词使用全局随机（接口支持的类型太少，无法一一对应）。
    /// </summary>
    private static readonly (string Pattern, string Category)[] CategoryMap =
    [
        // 动漫 / 二次元系
        ("anime", "anime"),
        ("acg", "anime"),
        ("illustration", "anime"),
        ("插画", "anime"),
        ("cyberpunk", "anime"),
        ("赛博朋克", "anime"),
        ("动漫", "anime"),
        ("二次元", "anime"),

        // 游戏系
        ("game", "game"),
        ("游戏", "game"),

        // 自然 / 风景系（该接口把动物、星空、宇宙等素材一并归入 nature）
        ("nature", "nature"),
        ("landscape", "nature"),
        ("animal", "nature"),
        ("animals", "nature"),
        ("starry", "nature"),
        ("space", "nature"),
        ("sky", "nature"),
        ("风景", "nature"),
        ("动物", "nature"),
        ("星空", "nature"),
        ("宇宙", "nature")
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OspyoWallpaperProvider> _logger;

    public OspyoWallpaperProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<OspyoWallpaperProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Ospyo壁纸";

    /// <inheritdoc />
    public bool CanHandle(WallpaperQuery query) => true;

    /// <inheritdoc />
    public async Task<WallpaperCandidate?> GetRandomAsync(
        WallpaperQuery query,
        CancellationToken cancellationToken = default)
    {
        var category = KeywordCategoryMatcher.Match(query.Keyword, CategoryMap);

        // 优先按类别取图，失败时退化为全局随机，尽可能拿到一张图。
        var requestUrls = new List<string>();
        if (category is not null)
        {
            requestUrls.Add($"{BaseUrl}/{category}");
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
                _logger.LogWarning("Ospyo 未返回可用图片：{Keyword}", query.Keyword);
                return null;
            }

            _logger.LogDebug("Ospyo 返回壁纸（{Category}）：{Url}", category ?? "全局随机", url);
            return new WallpaperCandidate(url, Name, query.Category, query.Keyword, 0, 0);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogWarning(ex, "Ospyo 请求失败：{Keyword}", query.Keyword);
            return null;
        }
    }
}
