using System.Runtime.InteropServices;

namespace DailyWallpaper.Platforms.Windows;

/// <summary>
/// Windows 系统托盘图标（需求 3.3.2）。
/// </summary>
/// <remarks>
/// 通过创建一个“仅消息窗口”接收托盘回调消息，再弹出右键菜单。
/// 该窗口与 MAUI 的 WinUI 主窗口运行在同一 UI 线程上，因此可以复用 WinUI 的消息循环，
/// 不需要额外的线程或消息泵。
/// </remarks>
internal sealed class WindowsTrayIcon : IDisposable
{
    /// <summary>显示主窗口。</summary>
    public const int CommandShowWindow = 1001;

    /// <summary>立即切换壁纸。</summary>
    public const int CommandSwitchNow = 1002;

    /// <summary>退出应用。</summary>
    public const int CommandExit = 1003;

    private const string WindowClassName = "DailyWallpaper.TrayMessageWindow";
    private const uint TrayIconId = 1;

    private readonly string _tooltip;

    // 必须保持委托实例的强引用，否则窗口过程会被 GC 回收，导致进程崩溃。
    private readonly WindowsNativeMethods.WindowProc _windowProc;

    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _icon = IntPtr.Zero;
    private bool _iconAdded;
    private bool _disposed;

    public WindowsTrayIcon(string tooltip)
    {
        // szTip 最多 127 个字符。
        _tooltip = tooltip.Length > 100 ? tooltip[..100] : tooltip;
        _windowProc = WindowProc;
    }

    /// <summary>用户点击托盘图标（左键）。</summary>
    public event EventHandler? ShowWindowRequested;

    /// <summary>用户选择“立即切换”。</summary>
    public event EventHandler? SwitchNowRequested;

    /// <summary>用户选择“退出应用”。</summary>
    public event EventHandler? ExitRequested;

    /// <summary>创建托盘图标；失败时返回 <c>false</c>（不影响应用主流程）。</summary>
    public bool TryCreate()
    {
        try
        {
            var moduleHandle = WindowsNativeMethods.GetModuleHandle(null);

            var windowClass = new WindowsNativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WindowsNativeMethods.WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_windowProc),
                hInstance = moduleHandle,
                lpszClassName = WindowClassName
            };

            var atom = WindowsNativeMethods.RegisterClassEx(ref windowClass);
            if (atom == 0)
            {
                // 已经注册过（例如应用重启后仍驻留）时继续使用同名类。
                var error = Marshal.GetLastWin32Error();
                if (error != 1410 /* ERROR_CLASS_ALREADY_EXISTS */)
                {
                    return false;
                }
            }

            _hwnd = WindowsNativeMethods.CreateWindowEx(
                0,
                WindowClassName,
                null,
                0,
                0,
                0,
                0,
                0,
                WindowsNativeMethods.HWND_MESSAGE,
                IntPtr.Zero,
                moduleHandle,
                IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                return false;
            }

            _icon = LoadApplicationIcon();

            var data = new WindowsNativeMethods.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<WindowsNativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = TrayIconId,
                uFlags = WindowsNativeMethods.NIF_MESSAGE | WindowsNativeMethods.NIF_ICON | WindowsNativeMethods.NIF_TIP,
                uCallbackMessage = (uint)WindowsNativeMethods.TrayCallbackMessage,
                hIcon = _icon,
                szTip = _tooltip
            };

            _iconAdded = WindowsNativeMethods.Shell_NotifyIcon(WindowsNativeMethods.NIM_ADD, ref data);
            return _iconAdded;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_iconAdded)
        {
            var data = new WindowsNativeMethods.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<WindowsNativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = TrayIconId,
                szTip = string.Empty
            };

            WindowsNativeMethods.Shell_NotifyIcon(WindowsNativeMethods.NIM_DELETE, ref data);
            _iconAdded = false;
        }

        if (_hwnd != IntPtr.Zero)
        {
            WindowsNativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    /// <summary>优先使用应用自身的可执行文件图标，失败时退回系统默认图标。</summary>
    private static IntPtr LoadApplicationIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                var extracted = WindowsNativeMethods.ExtractIconEx(exePath, 0, out var large, out var small, 1);
                if (extracted > 0)
                {
                    if (small != IntPtr.Zero)
                    {
                        return small;
                    }

                    if (large != IntPtr.Zero)
                    {
                        return large;
                    }
                }
            }
        }
        catch
        {
            // 忽略，使用默认图标。
        }

        return WindowsNativeMethods.LoadIcon(IntPtr.Zero, new IntPtr(WindowsNativeMethods.IDI_APPLICATION));
    }

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == (uint)WindowsNativeMethods.TrayCallbackMessage)
        {
            var notification = (int)lParam & 0xFFFF;
            switch (notification)
            {
                case WindowsNativeMethods.WM_LBUTTONUP:
                    ShowWindowRequested?.Invoke(this, EventArgs.Empty);
                    return IntPtr.Zero;

                case WindowsNativeMethods.WM_RBUTTONUP:
                    ShowContextMenu();
                    return IntPtr.Zero;
            }
        }
        else if (msg == WindowsNativeMethods.WM_DESTROY)
        {
            WindowsNativeMethods.PostQuitMessage(0);
            return IntPtr.Zero;
        }

        return WindowsNativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var menu = WindowsNativeMethods.CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            WindowsNativeMethods.AppendMenu(menu, WindowsNativeMethods.MF_STRING, CommandShowWindow, "显示主窗口");
            WindowsNativeMethods.AppendMenu(menu, WindowsNativeMethods.MF_STRING, CommandSwitchNow, "立即切换壁纸");
            WindowsNativeMethods.AppendMenu(menu, WindowsNativeMethods.MF_SEPARATOR, 0, null);
            WindowsNativeMethods.AppendMenu(menu, WindowsNativeMethods.MF_STRING, CommandExit, "退出应用");

            if (!WindowsNativeMethods.GetCursorPos(out var point))
            {
                return;
            }

            // TrackPopupMenu 需要窗口位于前台，否则菜单不会自动关闭。
            WindowsNativeMethods.SetForegroundWindow(_hwnd);

            var command = WindowsNativeMethods.TrackPopupMenu(
                menu,
                WindowsNativeMethods.TPM_LEFTALIGN | WindowsNativeMethods.TPM_RIGHTBUTTON
                | WindowsNativeMethods.TPM_RETURNCMD | WindowsNativeMethods.TPM_NONOTIFY,
                point.X,
                point.Y,
                0,
                _hwnd,
                IntPtr.Zero);

            // 文档要求的收尾消息，确保菜单消失。
            WindowsNativeMethods.PostMessage(_hwnd, (uint)WindowsNativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            switch (command)
            {
                case CommandShowWindow:
                    ShowWindowRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case CommandSwitchNow:
                    SwitchNowRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case CommandExit:
                    ExitRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
        finally
        {
            WindowsNativeMethods.DestroyMenu(menu);
        }
    }
}
