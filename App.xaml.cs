using DailyWallpaper.Models;
using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace DailyWallpaper;

/// <summary>
/// 应用生命周期管理：窗口创建时完成一次初始化（加载设置与日志、启动后台服务、安排定时任务）。
/// </summary>
public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = _services.GetRequiredService<AppShell>();
        var window = new Window(shell)
        {
            Title = "DailyWallpaper 每日壁纸"
        };

#if WINDOWS
        // Windows 端需要常驻系统托盘（需求 3.3.2）：等 WinUI 窗口（Handler）创建后再挂接。
        window.HandlerChanged += OnWindowHandlerChanged;
#endif

        // 初始化在后台执行，避免阻塞首屏渲染。
        _ = InitializeAsync();

        return window;
    }

#if WINDOWS
    private void OnWindowHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is not Window window ||
            window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window winuiWindow)
        {
            return;
        }

        window.HandlerChanged -= OnWindowHandlerChanged;
        Platforms.Windows.WindowsAppIntegration.Attach(winuiWindow);
    }
#endif

    /// <summary>
    /// 启动初始化（需求 3.3 / 3.4）：
    /// 读取本地设置与日志 → 恢复开机自启状态 → 启动后台常驻 → 按上次切换时间重新安排定时任务。
    /// </summary>
    private async Task InitializeAsync()
    {
        var log = _services.GetRequiredService<ILogService>();
        var settings = _services.GetRequiredService<ISettingsService>();

        try
        {
            await log.LoadAsync().ConfigureAwait(false);
            await settings.LoadAsync().ConfigureAwait(false);

            var device = _services.GetRequiredService<IDeviceDisplayService>();
            var setter = _services.GetRequiredService<IWallpaperSetter>();

            log.Write(
                WallpaperLogLevel.Info,
                $"应用已启动（{setter.PlatformName}，目标分辨率 {device.ScreenWidth}×{device.ScreenHeight}）",
                source: "应用");

            var startup = _services.GetRequiredService<IStartupService>();
            log.Write(
                WallpaperLogLevel.Info,
                $"开机自启：{(startup.IsEnabled ? "已开启" : "未开启")}",
                source: "应用");

            var background = _services.GetRequiredService<IBackgroundScheduler>();
            if (settings.Current.IsEnabled)
            {
                // Windows 启动内部定时器；Android 启动前台服务。
                await background.StartAsync().ConfigureAwait(false);
            }
            else
            {
                log.Write(WallpaperLogLevel.Info, "自动切换处于暂停状态，未启动后台任务", source: "应用");
            }

            // 根据上次切换时间计算下一次切换时间，并补做一次“错过的切换”。
            var scheduler = _services.GetRequiredService<IWallpaperScheduler>();
            await scheduler.InitializeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Write(WallpaperLogLevel.Error, "应用初始化失败", ex, source: "应用");
        }
    }
}
