using DailyWallpaper.Models;
using DailyWallpaper.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using WinUIWindow = Microsoft.UI.Xaml.Window;

namespace DailyWallpaper.Platforms.Windows;

/// <summary>
/// Windows 端窗口与托盘集成（需求 3.3.2）：
/// 应用启动后常驻系统托盘，关闭主窗口只是隐藏到托盘，只有托盘菜单中的“退出应用”才会真正结束进程。
/// </summary>
internal static class WindowsAppIntegration
{
    private static WindowsTrayIcon? _trayIcon;
    private static WinUIWindow? _window;
    private static IntPtr _windowHandle = IntPtr.Zero;

    /// <summary>是否正在由用户主动退出（用于区分“关闭窗口”与“退出应用”）。</summary>
    public static bool IsExiting { get; private set; }

    /// <summary>在主窗口创建后挂接托盘与关闭行为。</summary>
    public static void Attach(WinUIWindow window)
    {
        try
        {
            _window = window;
            _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);

            _trayIcon = new WindowsTrayIcon("DailyWallpaper 每日壁纸");
            _trayIcon.ShowWindowRequested += (_, _) => ShowMainWindow();
            _trayIcon.SwitchNowRequested += OnTraySwitchNowRequested;
            _trayIcon.ExitRequested += (_, _) => ExitApplication();

            if (_trayIcon.TryCreate())
            {
                WriteLog(WallpaperLogLevel.Info, "已创建系统托盘图标，关闭主窗口将最小化到托盘");
            }
            else
            {
                WriteLog(WallpaperLogLevel.Warning, "创建系统托盘图标失败，应用仍可正常使用");
            }

            window.AppWindow.Closing += OnWindowClosing;
        }
        catch (Exception ex)
        {
            WriteLog(WallpaperLogLevel.Warning, "初始化托盘集成失败", ex);
        }
    }

    /// <summary>显示主窗口（从托盘恢复）。</summary>
    public static void ShowMainWindow()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        WindowsNativeMethods.ShowWindow(_windowHandle, WindowsNativeMethods.SW_RESTORE);
        WindowsNativeMethods.ShowWindow(_windowHandle, WindowsNativeMethods.SW_SHOW);
        WindowsNativeMethods.SetForegroundWindow(_windowHandle);
    }

    /// <summary>真正退出应用：移除托盘图标后关闭窗口。</summary>
    public static void ExitApplication()
    {
        if (IsExiting)
        {
            return;
        }

        IsExiting = true;
        WriteLog(WallpaperLogLevel.Info, "用户从托盘菜单退出应用");

        _trayIcon?.Dispose();
        _trayIcon = null;

        if (_window is not null)
        {
            _window.AppWindow.Closing -= OnWindowClosing;
        }

        Microsoft.UI.Xaml.Application.Current?.Exit();
    }

    private static void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (IsExiting)
        {
            return;
        }

        // 需求：关闭主窗口时最小化到托盘，而不是退出（可在设置页关闭该行为）。
        if (ShouldMinimizeToTray())
        {
            args.Cancel = true;
            WindowsNativeMethods.ShowWindow(_windowHandle, WindowsNativeMethods.SW_HIDE);
        }
    }

    private static bool ShouldMinimizeToTray()
    {
        try
        {
            var settings = MauiProgram.Services?.GetService<ISettingsService>();
            return settings?.Current.MinimizeToTrayOnClose ?? true;
        }
        catch
        {
            return true;
        }
    }

    private static void OnTraySwitchNowRequested(object? sender, EventArgs e) =>
        _ = Task.Run(async () =>
        {
            try
            {
                var rotation = MauiProgram.Services?.GetService<IWallpaperRotationService>();
                if (rotation is not null)
                {
                    await rotation.SwitchNowAsync(isAutomatic: false).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                WriteLog(WallpaperLogLevel.Error, "从托盘触发立即切换失败", ex);
            }
        });

    private static void WriteLog(WallpaperLogLevel level, string message, Exception? exception = null)
    {
        try
        {
            var log = MauiProgram.Services?.GetService<ILogService>();
            if (log is null)
            {
                return;
            }

            if (exception is null)
            {
                log.Write(level, message, source: "托盘");
            }
            else
            {
                log.Write(level, message, exception, source: "托盘");
            }
        }
        catch
        {
            // 日志失败不影响主流程。
        }
    }
}
