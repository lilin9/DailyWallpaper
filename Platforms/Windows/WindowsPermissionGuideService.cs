using DailyWallpaper.Services.Platform;

namespace DailyWallpaper.Platforms.Windows;

/// <summary>
/// Windows 端不需要权限引导页（需求 5.4 只针对 Android）。
/// </summary>
public sealed class WindowsPermissionGuideService : IPermissionGuideService
{
    /// <inheritdoc />
    public bool IsSupported => false;

    /// <inheritdoc />
    public Task<IReadOnlyList<PermissionGuideItem>> GetItemsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PermissionGuideItem>>(Array.Empty<PermissionGuideItem>());

    /// <inheritdoc />
    public Task<bool> ExecuteAsync(string itemId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
