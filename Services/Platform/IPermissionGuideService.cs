namespace DailyWallpaper.Services.Platform;

/// <summary>
/// 权限引导项的状态（需求 5.4：权限引导页）。
/// </summary>
/// <param name="Id">唯一标识，用于触发对应的跳转动作。</param>
/// <param name="Title">标题，例如“开启自启动”。</param>
/// <param name="Description">说明文本。</param>
/// <param name="ActionText">按钮文本。</param>
/// <param name="IsCompleted">是否已完成。</param>
public sealed record PermissionGuideItem(
    string Id,
    string Title,
    string Description,
    string ActionText,
    bool IsCompleted);

/// <summary>
/// 权限引导服务：Android 跳转到系统页面，其它平台返回空列表。
/// </summary>
public interface IPermissionGuideService
{
    /// <summary>当前平台是否需要展示权限引导页。</summary>
    bool IsSupported { get; }

    /// <summary>获取当前各引导项的完成状态。</summary>
    Task<IReadOnlyList<PermissionGuideItem>> GetItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>执行引导动作（跳转到系统设置页面或请求权限）。</summary>
    Task<bool> ExecuteAsync(string itemId, CancellationToken cancellationToken = default);
}
