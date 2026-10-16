using System.Text.Json.Serialization;

namespace DailyWallpaper.Models;

/// <summary>日志级别。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WallpaperLogLevel>))]
public enum WallpaperLogLevel
{
    /// <summary>普通信息。</summary>
    Info = 0,

    /// <summary>操作成功。</summary>
    Success = 1,

    /// <summary>警告，例如网络失败后保持原壁纸。</summary>
    Warning = 2,

    /// <summary>错误，例如图片处理或设置壁纸失败。</summary>
    Error = 3
}

/// <summary>
/// 一条壁纸切换日志。以 JSON Lines 形式追加写入 <c>AppDataDirectory/logs.jsonl</c>，可在应用内查看。
/// </summary>
public sealed class WallpaperLogEntry
{
    /// <summary>记录时间（本地时间）。</summary>
    public DateTime Time { get; set; } = DateTime.Now;

    /// <summary>级别。</summary>
    public WallpaperLogLevel Level { get; set; } = WallpaperLogLevel.Info;

    /// <summary>触发本次切换的类别名称。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>图片来源（壁纸源名称或 URL）。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>简短描述。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>异常信息等详细信息。</summary>
    public string? Detail { get; set; }

    /// <summary>界面上显示的一行文本。</summary>
    [JsonIgnore]
    public string Display =>
        $"[{Time:MM-dd HH:mm:ss}] {LevelText}｜类别：{ValueOrDash(Category)}｜来源：{ValueOrDash(Source)}｜{Message}";

    /// <summary>级别的中文文本。</summary>
    [JsonIgnore]
    public string LevelText => Level switch
    {
        WallpaperLogLevel.Success => "成功",
        WallpaperLogLevel.Warning => "警告",
        WallpaperLogLevel.Error => "错误",
        _ => "信息"
    };

    private static string ValueOrDash(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
}
