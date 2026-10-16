namespace DailyWallpaper.Services.Providers;

/// <summary>
/// 解析“随机图片接口”最终指向的真实图片地址。
/// </summary>
/// <remarks>
/// 本项目使用的两个图源都是先返回 <c>302</c>、再跳转到真实图片地址（部分还会连跳多次，例如 Uapi 的
/// <c>landscape</c> 类别会再跳到第三方图床）。这里用 <see cref="HttpCompletionOption.ResponseHeadersRead"/>
/// 只读取响应头，再通过 <see cref="HttpResponseMessage.RequestMessage"/> 的最终 URI 得到图片地址，
/// 从而避免把整张图片下载两遍（一次用于解析地址、一次用于真正下载）。
/// </remarks>
internal static class ImageSourceResolver
{
    /// <summary>
    /// 请求 <paramref name="requestUrl"/> 并返回它最终指向的图片地址；不是图片或请求失败时返回 <c>null</c>。
    /// </summary>
    public static async Task<string?> ResolveImageUrlAsync(
        HttpClient client,
        string requestUrl,
        CancellationToken cancellationToken)
    {
        using var response = await client
            .GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        // 只接受真正的图片响应，避免把 HTML 错误页当成图片地址交给下载器。
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is null || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // 自动跟随重定向后，本次请求的最终地址就是真实图片地址。
        return response.RequestMessage?.RequestUri?.ToString() ?? requestUrl;
    }

    /// <summary>
    /// 依次尝试 <paramref name="requestUrls"/>，并尽量避开 <paramref name="recentKeys"/> 中最近用过的图片。
    /// </summary>
    /// <param name="requestUrls">按优先级排列的候选请求地址（例如“指定类别”在前、“全局随机”在后）。</param>
    /// <param name="recentKeys">最近使用过的图片地址，用于避免连续切换出现同一张图。</param>
    /// <param name="maxAttemptsPerUrl">每个地址最多请求几次（仅用于换一张不重复的图片）。</param>
    /// <remarks>
    /// 某个地址返回错误（例如类别已下线导致 <c>404</c>）时会立即尝试下一个地址，不会反复重试。
    /// 全部尝试都拿到同一张已用过的图片时，返回最后一张，保证“有图可用”优先于“绝对不重复”。
    /// </remarks>
    public static async Task<string?> ResolveRandomImageUrlAsync(
        HttpClient client,
        IReadOnlyList<string> requestUrls,
        IReadOnlyCollection<string> recentKeys,
        int maxAttemptsPerUrl,
        CancellationToken cancellationToken)
    {
        string? lastResolved = null;
        var attempts = Math.Max(1, maxAttemptsPerUrl);

        foreach (var requestUrl in requestUrls)
        {
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var url = await ResolveImageUrlAsync(client, requestUrl, cancellationToken).ConfigureAwait(false);
                if (url is null)
                {
                    // 该地址不可用（网络异常、类别已下线等），直接换下一个候选地址。
                    break;
                }

                lastResolved = url;
                if (!recentKeys.Contains(url))
                {
                    return url;
                }
            }
        }

        return lastResolved;
    }
}
