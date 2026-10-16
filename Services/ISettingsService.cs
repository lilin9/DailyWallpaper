using DailyWallpaper.Models;

namespace DailyWallpaper.Services;

/// <summary>
/// 本地设置的读写服务（JSON 文件，保存在应用私有目录，不涉及任何云端同步）。
/// </summary>
public interface ISettingsService
{
    /// <summary>当前设置实例（可被多个 ViewModel 直接读取）。</summary>
    AppSettings Current { get; }

    /// <summary>设置文件完整路径。</summary>
    string SettingsFilePath { get; }

    /// <summary>从磁盘加载设置；文件不存在或损坏时使用默认值。</summary>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>把 <see cref="Current"/> 写入磁盘。</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>把指定设置写入磁盘并更新 <see cref="Current"/>。</summary>
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
