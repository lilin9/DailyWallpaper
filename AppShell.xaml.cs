using DailyWallpaper.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DailyWallpaper;

/// <summary>
/// 应用导航外壳：底部（Windows 为左侧）标签页 + 权限引导页路由。
/// </summary>
public partial class AppShell : Shell
{
    /// <summary>权限引导页路由（仅 Android 展示具体内容，其它平台给出说明）。</summary>
    public const string PermissionsRoute = "permissions";

    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        HomeTab.Content = services.GetRequiredService<HomePage>();
        CategoriesTab.Content = services.GetRequiredService<CategoriesPage>();
        IntervalTab.Content = services.GetRequiredService<IntervalPage>();
        LogsTab.Content = services.GetRequiredService<LogsPage>();
        SettingsTab.Content = services.GetRequiredService<SettingsPage>();

        Routing.RegisterRoute(PermissionsRoute, typeof(PermissionsPage));
    }
}
