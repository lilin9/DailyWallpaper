using System.Text.Json.Serialization;
using DailyWallpaper.Models;

namespace DailyWallpaper.Services;

/// <summary>
/// System.Text.Json 源生成上下文：避免反射序列化，兼容 Android Release 的裁剪 / AOT。
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(WallpaperCategoryConfig))]
[JsonSerializable(typeof(WallpaperLogEntry))]
[JsonSerializable(typeof(WallpaperCacheIndex))]
[JsonSerializable(typeof(List<WallpaperLogEntry>))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
