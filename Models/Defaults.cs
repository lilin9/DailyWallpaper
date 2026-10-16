namespace DailyWallpaper.Models;

/// <summary>
/// 类别相关规则的集中定义。
/// </summary>
/// <remarks>
/// 需求文档 3.1.1 要求默认预设类别最多 9 个（本项目取满 9 个：动漫 / 动物 / 宇宙 / 星空 / 风景 / AI / 二次元 / 插画 / 赛博朋克），
/// 3.1.3 又要求“所有类别总数 1 到 9 个”。由于默认类别不允许删除，若把 9 个默认类别与自定义类别一起计入总数，
/// 自定义类别将永远无法添加，与“用户可动态添加自定义类别”相矛盾。
/// 因此这里采用如下解释（README 中同样有说明）：
/// <list type="bullet">
///   <item><description>被勾选（启用）的类别数量必须在 1~9 个之间，这是硬性约束；</description></item>
///   <item><description>自定义类别最多 9 个，避免列表无限膨胀；</description></item>
///   <item><description>默认类别始终保留在列表中，只能取消勾选，不能修改或删除。</description></item>
/// </list>
/// </remarks>
public static class Defaults
{
    /// <summary>同时可启用的类别数量上限。</summary>
    public const int MaxSelectedCategories = 9;

    /// <summary>自定义类别数量上限。</summary>
    public const int MaxCustomCategories = 9;

    /// <summary>用户输入的类别名称最大长度（字符数）。</summary>
    public const int MaxCategoryNameLength = 5;

    /// <summary>同时可启用的类别数量下限。</summary>
    public const int MinSelectedCategories = 1;

    /// <summary>本地缓存壁纸默认张数。</summary>
    public const int DefaultCacheSize = 5;
}
