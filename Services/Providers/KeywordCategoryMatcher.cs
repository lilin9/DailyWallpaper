namespace DailyWallpaper.Services.Providers;

/// <summary>
/// 把应用中的壁纸关键词（例如 <c>categories.json</c> 里的 <c>anime</c>、<c>starry sky</c>）映射为图源支持的类别参数。
/// </summary>
/// <remarks>
/// 匹配规则针对中英文分别处理：
/// <list type="bullet">
///   <item><description>拉丁文关键词按“整词”匹配，避免 <c>ai</c> 误命中 <c>rain</c> 之类的包含关系；</description></item>
///   <item><description>中文关键词按“包含”匹配，因为中文没有天然的词间分隔符（如“日本动漫”应命中“动漫”）。</description></item>
/// </list>
/// 匹配在关键词的小写形式上、按表内顺序进行，先命中者胜出。
/// </remarks>
internal static class KeywordCategoryMatcher
{
    /// <summary>关键词切分符：空格、逗号（中英文）、加号、竖线、斜杠、下划线、连字符、点。</summary>
    private static readonly char[] Separators = [' ', ',', '，', '+', '|', '/', '\\', '_', '-', '.'];

    /// <summary>
    /// 在映射表中查找 <paramref name="keyword"/> 对应的类别；没有匹配时返回 <c>null</c>。
    /// </summary>
    /// <param name="keyword">类别映射出的搜索关键词。</param>
    /// <param name="table">映射表，元素为（匹配模式, 目标类别）。</param>
    public static string? Match(string? keyword, IReadOnlyList<(string Pattern, string Category)> table)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        var lowered = keyword.ToLowerInvariant();
        var tokens = lowered.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var (pattern, category) in table)
        {
            var matched = IsCjk(pattern)
                ? lowered.Contains(pattern, StringComparison.Ordinal)
                : tokens.Contains(pattern, StringComparer.Ordinal);

            if (matched)
            {
                return category;
            }
        }

        return null;
    }

    /// <summary>判断是否为中日韩字符（这些字符没有词间分隔符，需要按包含关系匹配）。</summary>
    private static bool IsCjk(string value) => value.Any(static ch => ch >= 0x2E80);
}
