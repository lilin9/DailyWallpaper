using System.Text;
using DailyWallpaper.Models;

namespace DailyWallpaper.Services;

/// <summary>类别名称校验结果。</summary>
/// <param name="IsValid">是否通过校验。</param>
/// <param name="ErrorMessage">未通过时的中文提示。</param>
public sealed record CategoryValidationResult(bool IsValid, string? ErrorMessage)
{
    /// <summary>校验通过。</summary>
    public static CategoryValidationResult Ok { get; } = new(true, null);

    /// <summary>校验失败。</summary>
    public static CategoryValidationResult Fail(string message) => new(false, message);
}

/// <summary>
/// 自定义类别名称的校验规则（需求 3.1.3）。
/// </summary>
public static class CategoryNameValidator
{
    /// <summary>
    /// 校验用户输入的新类别名称。
    /// 规则：非空、长度不超过 5 个字符、与默认类别和已有自定义类别都不重复（相同或包含关系都算重复）。
    /// </summary>
    /// <param name="input">用户输入。</param>
    /// <param name="existingNames">已存在的全部类别名称（默认类别 + 自定义类别）。</param>
    public static CategoryValidationResult Validate(string? input, IEnumerable<string> existingNames)
    {
        var name = (input ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return CategoryValidationResult.Fail("请输入类别名称。");
        }

        var length = CountTextCharacters(name);
        if (length > Defaults.MaxCategoryNameLength)
        {
            return CategoryValidationResult.Fail(
                $"类别名称最多 {Defaults.MaxCategoryNameLength} 个字符，当前为 {length} 个。");
        }

        foreach (var existing in existingNames)
        {
            if (IsDuplicate(name, existing))
            {
                return CategoryValidationResult.Fail($"“{name}”与已有类别“{existing}”重复（相同或互相包含都视为重复）。");
            }
        }

        return CategoryValidationResult.Ok;
    }

    /// <summary>
    /// 判断两个类别名称是否重复：
    /// 忽略首尾空格、忽略空格差异与大小写后完全相同，或存在包含关系（例如“动漫 / 日本动漫”“AI / AI艺术”）。
    /// </summary>
    public static bool IsDuplicate(string left, string right)
    {
        var a = Normalize(left);
        var b = Normalize(right);

        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        return string.Equals(a, b, StringComparison.Ordinal)
               || a.Contains(b, StringComparison.Ordinal)
               || b.Contains(a, StringComparison.Ordinal);
    }

    /// <summary>按 Unicode 码位计数，避免 emoji 等字符被误判为多个字符。</summary>
    public static int CountTextCharacters(string value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateRunes())
        {
            count++;
        }

        return count;
    }

    private static string Normalize(string value) =>
        value.Trim().Replace(" ", string.Empty).ToLowerInvariant();
}
