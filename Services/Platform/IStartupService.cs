namespace DailyWallpaper.Services.Platform;

/// <summary>
/// 开机自启设置。
/// Android：只能引导用户在系统“自启动管理”中开启；Windows：写入注册表 Run 键 / 启动文件夹。
/// </summary>
public interface IStartupService
{
    /// <summary>当前平台是否支持应用内直接开关开机自启。</summary>
    bool IsSupported { get; }

    /// <summary>开机自启是否已开启。</summary>
    bool IsEnabled { get; }

    /// <summary>设置开机自启；返回设置后的状态。</summary>
    Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>给用户看的补充说明（例如 Android 需要手动去系统设置开启）。</summary>
    string Description { get; }
}
