namespace DailyWallpaper.Models;

/// <summary>
/// 应用全部本地设置。
/// 序列化后保存在 <c>FileSystem.AppDataDirectory/settings.json</c>，不涉及任何网络上传。
/// </summary>
public sealed class AppSettings
{
    /// <summary>用户自定义类别（可增删）。默认类别不保存在这里，而是来自 categories.json。</summary>
    public List<string> CustomCategories { get; set; } = new();

    /// <summary>当前勾选（启用）的类别名称，必须至少 1 个、最多 <see cref="Defaults.MaxSelectedCategories"/> 个。</summary>
    public List<string> SelectedCategories { get; set; } = new();

    /// <summary>自动切换间隔。</summary>
    public SwitchInterval Interval { get; set; } = SwitchInterval.OneDay;

    /// <summary>上一次成功切换壁纸的时间（本地时间）。</summary>
    public DateTime? LastSwitchTime { get; set; }

    /// <summary>下一次计划切换时间（本地时间）。</summary>
    public DateTime? NextSwitchTime { get; set; }

    /// <summary>最近一次成功设置的壁纸文件路径。</summary>
    public string? LastWallpaperPath { get; set; }

    /// <summary>最近一次壁纸所属类别，便于日志排查。</summary>
    public string? LastWallpaperCategory { get; set; }

    /// <summary>最近一次壁纸的原始来源 URL。</summary>
    public string? LastWallpaperSourceUrl { get; set; }

    /// <summary>最近一次壁纸所用壁纸源名称。</summary>
    public string? LastWallpaperProvider { get; set; }

    /// <summary>是否启用自动切换（关闭后仍可使用“立即切换”）。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>是否开机自启。</summary>
    public bool RunAtStartup { get; set; } = true;

    /// <summary>Windows：关闭主窗口时最小化到托盘而不是退出。</summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>是否同时设置锁屏壁纸（Android 有效；Windows 视系统 API 能力降级）。</summary>
    public bool SetLockScreen { get; set; } = true;

    /// <summary>本地缓存最近多少张壁纸（用于去重与断网备选）。</summary>
    public int CacheSize { get; set; } = 5;

    /// <summary>最近使用过的图片来源标识（URL 或文件名），用于避免连续切换时重复使用同一张图片。</summary>
    public List<string> RecentImageKeys { get; set; } = new();

    /// <summary>用户是否已完成 Android 权限引导。</summary>
    public bool PermissionGuideCompleted { get; set; }

    /// <summary>深拷贝，用于“保存 / 取消”编辑语义。</summary>
    public AppSettings Clone() => new()
    {
        CustomCategories = new List<string>(CustomCategories),
        SelectedCategories = new List<string>(SelectedCategories),
        Interval = Interval,
        LastSwitchTime = LastSwitchTime,
        NextSwitchTime = NextSwitchTime,
        LastWallpaperPath = LastWallpaperPath,
        LastWallpaperCategory = LastWallpaperCategory,
        LastWallpaperSourceUrl = LastWallpaperSourceUrl,
        LastWallpaperProvider = LastWallpaperProvider,
        IsEnabled = IsEnabled,
        RunAtStartup = RunAtStartup,
        MinimizeToTrayOnClose = MinimizeToTrayOnClose,
        SetLockScreen = SetLockScreen,
        CacheSize = CacheSize,
        RecentImageKeys = new List<string>(RecentImageKeys),
        PermissionGuideCompleted = PermissionGuideCompleted
    };

    /// <summary>把另一个实例的内容复制到当前实例（保持引用不变，便于 MVVM 绑定）。</summary>
    public void CopyFrom(AppSettings other)
    {
        CustomCategories = new List<string>(other.CustomCategories);
        SelectedCategories = new List<string>(other.SelectedCategories);
        Interval = other.Interval;
        LastSwitchTime = other.LastSwitchTime;
        NextSwitchTime = other.NextSwitchTime;
        LastWallpaperPath = other.LastWallpaperPath;
        LastWallpaperCategory = other.LastWallpaperCategory;
        LastWallpaperSourceUrl = other.LastWallpaperSourceUrl;
        LastWallpaperProvider = other.LastWallpaperProvider;
        IsEnabled = other.IsEnabled;
        RunAtStartup = other.RunAtStartup;
        MinimizeToTrayOnClose = other.MinimizeToTrayOnClose;
        SetLockScreen = other.SetLockScreen;
        CacheSize = other.CacheSize;
        RecentImageKeys = new List<string>(other.RecentImageKeys);
        PermissionGuideCompleted = other.PermissionGuideCompleted;
    }
}
