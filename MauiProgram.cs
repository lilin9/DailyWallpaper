using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;
using DailyWallpaper.Services.Providers;
using DailyWallpaper.ViewModels;
using DailyWallpaper.Views;
using Microsoft.Extensions.Logging;

#if ANDROID
using DailyWallpaper.Platforms.Android;
#elif WINDOWS
using DailyWallpaper.Platforms.Windows;
#endif

namespace DailyWallpaper;

/// <summary>
/// 应用入口：注册依赖注入容器、MVVM 视图与平台特定实现。
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// 全局服务容器。
    /// Android 的 <c>Service</c> / <c>BroadcastReceiver</c> 由系统实例化、无法声明构造函数参数，
    /// 因此它们通过这个属性解析所需的服务。
    /// </summary>
    public static IServiceProvider? Services { get; private set; }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        RegisterCoreServices(builder.Services);
        RegisterPlatformServices(builder.Services);
        RegisterViews(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // 供 Android 服务 / 广播接收器以及平台辅助类使用。
        Services = app.Services;

        return app;
    }

    /// <summary>注册与平台无关的核心服务。</summary>
    private static void RegisterCoreServices(IServiceCollection services)
    {
        // 具名 HttpClient：分别用于随机图片 API 查询与图片下载（超时时间不同）。
        services.AddHttpClient(WallpaperHttpClients.ApiQuery, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(25);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DailyWallpaper/1.0");
        });

        services.AddHttpClient(WallpaperHttpClients.Download, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(90);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DailyWallpaper/1.0");
        });

        // 单例：设置、日志、缓存等需要跨页面共享同一份状态。
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<IWallpaperCache, WallpaperCacheService>();
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
        services.AddSingleton<IWallpaperDownloader, WallpaperDownloader>();

        // 壁纸源：按注册顺序依次尝试，前一个失败自动降级到下一个。
        // 两者均为国内可直连的免密钥随机图片接口；都取不到图片时保持当前壁纸不变。
        services.AddSingleton<IWallpaperProvider, OspyoWallpaperProvider>();
        services.AddSingleton<IWallpaperProvider, UapisRandomImageProvider>();

        services.AddSingleton<IWallpaperRotationService, WallpaperRotationService>();
        services.AddSingleton<IWallpaperScheduler, WallpaperScheduler>();
    }

    /// <summary>注册当前平台的实现（Android / Windows）。</summary>
    private static void RegisterPlatformServices(IServiceCollection services)
    {
#if ANDROID
        services.AddSingleton<IWallpaperSetter, AndroidWallpaperSetter>();
        services.AddSingleton<IDeviceDisplayService, AndroidDeviceDisplayService>();
        services.AddSingleton<IStartupService, AndroidStartupService>();
        services.AddSingleton<IBackgroundScheduler, AndroidBackgroundScheduler>();
        services.AddSingleton<IPermissionGuideService, AndroidPermissionGuideService>();
#elif WINDOWS
        services.AddSingleton<IWallpaperSetter, WindowsWallpaperSetter>();
        services.AddSingleton<IDeviceDisplayService, WindowsDeviceDisplayService>();
        services.AddSingleton<IStartupService, WindowsStartupService>();
        services.AddSingleton<IBackgroundScheduler, WindowsBackgroundScheduler>();
        services.AddSingleton<IPermissionGuideService, WindowsPermissionGuideService>();
#else
        throw new PlatformNotSupportedException("DailyWallpaper 目前仅支持 Android 与 Windows 平台。");
#endif
    }

    /// <summary>注册页面与 ViewModel。</summary>
    private static void RegisterViews(IServiceCollection services)
    {
        // ViewModel：每次解析页面时创建一个新的实例。
        services.AddTransient<HomeViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<IntervalViewModel>();
        services.AddTransient<PermissionsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<LogsViewModel>();

        // 页面由 Shell 长期持有，注册为单例即可。
        services.AddSingleton<HomePage>();
        services.AddSingleton<CategoriesPage>();
        services.AddSingleton<IntervalPage>();
        services.AddSingleton<LogsPage>();
        services.AddSingleton<SettingsPage>();
        services.AddSingleton<PermissionsPage>();

        services.AddSingleton<AppShell>();
    }
}
