using System.Text.Json.Serialization;

namespace DailyWallpaper.Models;

/// <summary>
/// 壁纸自动切换的时间间隔。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SwitchInterval>))]
public enum SwitchInterval
{
    /// <summary>每 6 小时切换一次。</summary>
    SixHours = 0,

    /// <summary>每 12 小时切换一次。</summary>
    TwelveHours = 1,

    /// <summary>一天（24 小时）切换一次。</summary>
    OneDay = 2,

    /// <summary>一星期切换一次。</summary>
    OneWeek = 3,

    /// <summary>永久：不自动切换（仍可使用“立即切换”）。</summary>
    Never = 4
}

/// <summary>
/// <see cref="SwitchInterval"/> 的辅助方法：时间换算与显示名称。
/// </summary>
public static class SwitchIntervalExtensions
{
    /// <summary>界面上按顺序展示的全部可选间隔。</summary>
    public static readonly IReadOnlyList<SwitchInterval> All = new[]
    {
        SwitchInterval.SixHours,
        SwitchInterval.TwelveHours,
        SwitchInterval.OneDay,
        SwitchInterval.OneWeek,
        SwitchInterval.Never
    };

    /// <summary>
    /// 转换为 <see cref="TimeSpan"/>；“永久”返回 <c>null</c>，表示不自动切换。
    /// </summary>
    public static TimeSpan? ToTimeSpan(this SwitchInterval interval) => interval switch
    {
        SwitchInterval.SixHours => TimeSpan.FromHours(6),
        SwitchInterval.TwelveHours => TimeSpan.FromHours(12),
        SwitchInterval.OneDay => TimeSpan.FromDays(1),
        SwitchInterval.OneWeek => TimeSpan.FromDays(7),
        _ => null
    };

    /// <summary>中文显示名称。</summary>
    public static string ToDisplayName(this SwitchInterval interval) => interval switch
    {
        SwitchInterval.SixHours => "6 小时",
        SwitchInterval.TwelveHours => "12 小时",
        SwitchInterval.OneDay => "一天（24 小时）",
        SwitchInterval.OneWeek => "一星期",
        _ => "永久（不自动切换）"
    };

    /// <summary>简短显示名称（用于主页摘要）。</summary>
    public static string ToShortName(this SwitchInterval interval) => interval switch
    {
        SwitchInterval.SixHours => "每 6 小时",
        SwitchInterval.TwelveHours => "每 12 小时",
        SwitchInterval.OneDay => "每天",
        SwitchInterval.OneWeek => "每星期",
        _ => "永久"
    };
}
