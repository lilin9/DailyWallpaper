using DailyWallpaper.Models;
using DailyWallpaper.Services;
using DailyWallpaper.Services.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DailyWallpaper.Platforms.Windows;

/// <summary>
/// Windows 开机自启：写入 <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>。
/// </summary>
/// <remarks>
/// 使用当前用户注册表项（不需要管理员权限），也可以改用启动文件夹快捷方式。
/// </remarks>
public sealed class WindowsStartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DailyWallpaper";

    private readonly ILogService _log;
    private readonly ILogger<WindowsStartupService> _logger;

    public WindowsStartupService(ILogService log, ILogger<WindowsStartupService> logger)
    {
        _log = log;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public string Description =>
        "通过当前用户注册表 Run 键实现开机自启，开机后会常驻系统托盘；关闭主窗口只会最小化到托盘。";

    /// <inheritdoc />
    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(ValueName) is string value && value.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取开机自启注册表项失败");
                return false;
            }
        }
    }

    /// <inheritdoc />
    public Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (key is null)
            {
                _log.Write(WallpaperLogLevel.Warning, "无法打开开机自启注册表项");
                return Task.FromResult(IsEnabled);
            }

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    _log.Write(WallpaperLogLevel.Warning, "无法获取可执行文件路径，开机自启设置失败");
                    return Task.FromResult(IsEnabled);
                }

                // 加引号避免路径中包含空格时被错误解析。
                key.SetValue(ValueName, $"\"{exePath}\"", RegistryValueKind.String);
                _log.Write(WallpaperLogLevel.Info, "已开启开机自启");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                _log.Write(WallpaperLogLevel.Info, "已关闭开机自启");
            }
        }
        catch (Exception ex)
        {
            _log.Write(WallpaperLogLevel.Warning, "设置开机自启失败", ex);
        }

        return Task.FromResult(IsEnabled);
    }
}
