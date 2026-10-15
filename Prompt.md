# Prompt.md — 跨平台每日壁纸应用开发提示词

## 0. 给 Agent 的指令

你是一名资深 .NET MAUI / C# 工程师。请根据以下需求，设计并实现一个跨平台应用，优先支持 **Android（Xiaomi HyperOS 最新版）** 和 **Windows 11**。

要求：

- 使用 **.NET MAUI + C# + XAML** 开发。
- 使用 **MVVM** 架构，推荐 `CommunityToolkit.Mvvm`。
- 使用依赖注入、服务接口、平台特定实现。
- 输出完整可编译项目，包含解决方案、项目文件、平台配置、README。
- 不要实现用户登录、云同步、账号系统。
- 不要使用必须付费或必须申请密钥的 API 作为唯一壁纸源。
- 对 Android 保活能力要尽量强，但必须注明：受系统限制，无法保证 100% 无法被关闭。
- 所有代码和配置需附带必要注释。

---

## 1. 项目背景

小米手机自带的每日壁纸不好用：同一张壁纸反复切换，质量参差不齐，可选类别少。用户希望自己开发一个跨平台应用，用于每日自动更换手机壁纸，并且也可以在 Windows 11 电脑上运行。

应用名称可定为：**DailyWallpaper**。

---

## 2. 目标平台与技术栈

### 2.1 目标平台

- Android：Xiaomi HyperOS 最新版。
- Windows：Windows 11。
- 跨平台框架：.NET MAUI。
- 语言：C#。
- UI：XAML。
- 图片处理：SkiaSharp。
- 本地存储：MAUI `Preferences` / `SecureStorage` / 本地 JSON 文件。
- 网络：`HttpClient`。
- Android 平台调用：`WallpaperManager`、`Service`、`BroadcastReceiver`、`AlarmManager`、`Notification`。
- Windows 平台调用：Win32 API、系统托盘、注册表/启动文件夹、WinRT API（如锁屏壁纸设置可用）。

### 2.2 推荐 NuGet / 组件

- `CommunityToolkit.Mvvm`
- `CommunityToolkit.Maui`
- `SkiaSharp`
- `Microsoft.Extensions.Http`
- `Microsoft.Maui.Controls`
- Android 平台可配合 `Xamarin.AndroidX` 相关库。
- Windows 平台可配合 `CsWinRT` 调用 WinRT API。

---

## 3. 功能需求

### 3.1 壁纸类别选择

应用需要允许用户选择一个或多个壁纸类别。根据选中的多个类别，到网络上随机爬取 / 请求高清壁纸。

#### 3.1.1 默认预设类别

默认预设类别如下：

- 动漫
- 动物
- 宇宙
- 星空
- 风景
- AI

可额外推荐并预置最多 3 个类别，使默认预设总数不超过 9 个。推荐：

- 二次元
- 插画
- 赛博朋克

最终默认预设可以是：

1. 动漫
2. 动物
3. 宇宙
4. 星空
5. 风景
6. AI
7. 二次元
8. 插画
9. 赛博朋克

#### 3.1.2 默认类别规则

- 默认类别只允许取消选择，不允许修改、删除。
- 当用户点击“重置壁纸类别选项”时，恢复为默认预设类别。
- 默认类别即使被取消选择，也仍然保留在默认列表中。

#### 3.1.3 自定义类别规则

- 用户可动态添加或减少自定义类别。
- 自定义类别可以删除。
- 所有类别总数限制：**1 到 9 个**。
- 用户输入类别名称限制：**5 个字符以内**。
- 至少必须选择 1 个类别，否则不允许保存或开始自动切换。
- 所有选项不允许重复。
- 重复判断标准：
  - 字符与字符一一对应完全相同，视为重复。
  - 如果两个名称存在包含关系，也视为重复。
  - 示例：
    - “动漫”和“二次元”不重复。
    - “动漫”和“日本动漫”重复。
    - “AI”和“AI艺术”重复。
- 添加自定义类别时，必须同时检查默认类别和已有自定义类别。

#### 3.1.4 类别与网络关键词映射

每个类别需要映射为壁纸搜索关键词。例如：

| 类别 | 关键词 |
|---|---|
| 动漫 | anime |
| 动物 | animals |
| 宇宙 | space |
| 星空 | starry sky |
| 风景 | landscape |
| AI | ai art |
| 二次元 | anime |
| 插画 | illustration |
| 赛博朋克 | cyberpunk |

允许用户自定义类别时，如果无法自动映射，则直接使用用户输入作为搜索关键词。

>注意：类别可以本地配置在JSON文件中，以实现持久化存储。如果用户需要增改类别关键字，则对JSON文件进行编辑。
### 3.2 壁纸获取与设置

- 根据用户选中的多个类别，随机选择一个类别。
- 到网络随机获取高清壁纸。
- 壁纸源需要抽象为 `IWallpaperProvider`，方便后续替换。
- 默认使用无需密钥或可公开访问的随机图片 API。
- 示例可用源：
  - Wallhaven API：`https://wallhaven.cc/api/v1/search`
  - 其他公开随机图片 API。
- 下载高清图片，优先满足手机屏幕或电脑屏幕分辨率。
- 根据手机 / 电脑屏幕进行等比例缩放。
- 不考虑因为等比例修改图片导致的图片质量损失。
- 图片处理要求：
  - 使用 SkiaSharp。
  - 先按比例缩放到覆盖目标屏幕。
  - 再居中裁剪，避免变形。
  - 保存为临时文件或缓存文件。
- Android：
  - 同时设置桌面壁纸和锁屏壁纸。
  - 使用 `WallpaperManager`。
  - 桌面：`FLAG_SYSTEM`。
  - 锁屏：`FLAG_LOCK`。
- Windows：
  - 设置桌面壁纸。
  - 使用 Win32 API：`SystemParametersInfo(SPI_SETDESKWALLPAPER, ...)`。
  - 如果可行，尝试设置锁屏壁纸；如果系统 API 受限，则降级并提示用户。
  - 图片适配电脑屏幕分辨率。

---

### 3.3 后台常驻与开机自启

#### 3.3.1 Android 端

目标：尽可能实现后台常驻、开机自启、上滑一键清理后台任务无法关闭。

实现建议：

- 使用 **前台服务**。
- 前台服务必须显示持续通知。
- 服务返回 `START_STICKY`。
- 监听开机广播 `BOOT_COMPLETED`，实现开机自启。
- 引导用户：
  - 开启自启动权限。
  - 关闭电池优化，加入白名单。
  - 省电策略设置为“无限制”。
  - 允许通知。
  - 在最近任务中锁定应用。
- 监听 `onTaskRemoved`，尝试重新启动服务。
- 使用 `AlarmManager` 或前台服务内部定时器检查是否到达切换时间。
- Android 12+ 如需精确闹钟，申请 `SCHEDULE_EXACT_ALARM`。
- Android 13+ 申请 `POST_NOTIFICATIONS`。
- Android 14+ 前台服务需声明合适类型，例如 `specialUse`，并配置对应属性。

必须注明：

- 在非 Root 设备上，无法 100% 保证应用绝对无法被关闭。
- 通过上述组合策略，可以做到“极难被杀死”，但系统仍可能在某些极端情况下终止。

#### 3.3.2 Windows 端

- 应用启动后常驻系统托盘。
- 支持开机自启：
  - 写入注册表 `Run` 键，或
  - 在启动文件夹创建快捷方式。
- 使用定时器检查切换时间。
- 提供“退出应用”菜单，只有用户手动退出才关闭。
- 关闭主窗口时最小化到托盘，而不是退出。

#### 3.3.3 网络失败处理

- 如果网络连接失败，保持当前壁纸不变。
- 跳过本次切换。
- 记录日志。
- 等待下一个预设时间点再次尝试。
- 网络恢复后不立即切换，仍按预设时间切换。
- 可缓存最近若干张壁纸，避免重复下载，并在短暂断网时作为备选。

---

### 3.4 自定义壁纸切换时间

提供预设切换时间列表：

- 6 小时
- 12 小时
- 一天（24 小时）
- 一星期
- 永久（永久不自动切换）

规则：

- 用户选择后保存设置。
- “永久”模式下不自动切换。
- 即使选择“永久”，点击“立即切换”仍然可用。
- 应用重启、开机后，需要根据上次切换时间计算下次切换时间。

---

### 3.5 立即切换按钮

- 软件中提供一个按钮：“立即切换”。
- 点击后立即执行一次壁纸切换流程。
- 忽略当前定时设置。
- 如果网络失败，提示用户并保持当前壁纸。
- 切换成功后更新“上次切换时间”和“下次切换时间”。

---

### 3.6 跨平台一致性

- Android 端和 Windows 端功能保持一致。
- Windows 端同样支持：
  - 类别选择。
  - 自动切换时间。
  - 立即切换。
  - 后台常驻。
  - 开机自启。
  - 无登录。
- 差异仅在于：
  - Android 设置手机桌面 + 锁屏壁纸。
  - Windows 设置电脑桌面壁纸，锁屏壁纸视 API 能力降级。
  - 图片适配目标屏幕分辨率。

---

### 3.7 无用户登录

- 不设置用户登录。
- 不设置账号系统。
- 不设置云同步。
- 所有设置保存在本地。

---

## 4. 数据模型建议

```csharp
public enum SwitchInterval
{
    SixHours,
    TwelveHours,
    OneDay,
    OneWeek,
    Never
}

public class AppSettings
{
    //实际实现过程中可由JSON配置文件中读取
    public List<string> DefaultCategories { get; set; } = new()
    {
        "动漫", "动物", "宇宙", "星空", "风景", "AI", "二次元", "插画", "赛博朋克"
    };

    public List<string> CustomCategories { get; set; } = new();

    public List<string> SelectedCategories { get; set; } = new();

    public SwitchInterval Interval { get; set; } = SwitchInterval.OneDay;

    public DateTime? LastSwitchTime { get; set; }

    public DateTime? NextSwitchTime { get; set; }

    public string? LastWallpaperPath { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool RunAtStartup { get; set; } = true;
}

public class WallpaperCategory
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsSelected { get; set; }
}
```

---

## 5. UI 页面建议

至少包含以下页面：

1. **主页**
   - 当前壁纸预览。
   - 下次切换时间。
   - 启用 / 暂停自动切换。
   - “立即切换”按钮。

2. **类别设置页**
   - 默认类别复选框。
   - 自定义类别列表。
   - 添加自定义类别输入框。
   - 删除自定义类别按钮。
   - 重置为默认类别按钮。
   - 保存按钮。
   - 校验：至少选择 1 个类别，总数 1 到 9 个，名称不超过 5 字符，不允许重复或包含关系重复。

3. **切换时间设置页**
   - 6 小时。
   - 12 小时。
   - 一天。
   - 一星期。
   - 永久。

4. **权限引导页（Android）**
   - 引导开启自启动。
   - 引导关闭电池优化。
   - 引导允许通知。
   - 引导锁定后台。
   - 引导设置省电策略为无限制。

5. **设置页**
   - 开机自启开关。
   - 日志查看。
   - 关于。
   - 手动退出按钮。

6. **日志页**
   - 记录切换成功、失败、网络异常、权限异常等。

---

## 6. 平台特定实现建议

### 6.1 Android

- `AndroidManifest.xml` 权限：
  - `INTERNET`
  - `SET_WALLPAPER`
  - `RECEIVE_BOOT_COMPLETED`
  - `FOREGROUND_SERVICE`
  - `POST_NOTIFICATIONS`
  - `WAKE_LOCK`
  - `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`
  - `SCHEDULE_EXACT_ALARM`（如使用精确闹钟）
- 前台服务：
  - 创建 `WallpaperForegroundService`。
  - 通知渠道。
  - 定时检查。
- 开机广播：
  - `BootReceiver`。
  - 监听 `Intent.ActionBootCompleted`。
- 壁纸设置：
  - `WallpaperManager.SetBitmap(bitmap, null, true, WallpaperManager.FlagSystem)`。
  - `WallpaperManager.SetBitmap(bitmap, null, true, WallpaperManager.FlagLock)`。
- 保活：
  - `OnTaskRemoved` 中尝试重启服务。
  - 引导用户加入电池优化白名单。
  - 引导用户开启自启动和锁定后台。

### 6.2 Windows

- 系统托盘：
  - 使用 `NotifyIcon` 或 MAUI 社区工具包。
  - 关闭窗口时最小化到托盘。
- 开机自启：
  - 注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`。
  - 或启动文件夹快捷方式。
- 桌面壁纸：
  - `SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE)`。
- 锁屏壁纸：
  - 尝试使用 WinRT `Windows.System.UserProfile.LockScreen.SetImageFileAsync`。
  - 如果不可用，降级为仅设置桌面壁纸，并提示用户。
- 屏幕分辨率：
  - 使用 Win32 API 或 MAUI `DeviceDisplay` 获取。
- 定时器：
  - 使用 `System.Threading.Timer` 或 `DispatcherTimer`。

---

## 7. 网络与图片处理要求

- 使用 `HttpClient` 下载图片。
- 设置超时、重试、User-Agent。
- 图片格式支持：JPG、JPEG、PNG、WebP。
- 使用 SkiaSharp：
  - 加载原图。
  - 计算目标屏幕宽高。
  - 计算缩放比例：`Math.Max(targetWidth / srcWidth, targetHeight / srcHeight)`。
  - 等比例缩放至覆盖屏幕。
  - 居中裁剪。
  - 保存为临时文件。
- 缓存最近 N 张壁纸，例如 5 张。
- 避免连续切换时重复使用同一张图片。
- 记录图片来源和类别，便于排查。

---

## 8. 错误处理与日志

- 所有网络请求、图片处理、壁纸设置都需要 try-catch。
- 失败时记录日志，不崩溃。
- 网络失败保持当前壁纸。
- 权限不足时提示用户前往设置。
- 日志至少包含：
  - 时间。
  - 类别。
  - 图片来源。
  - 成功 / 失败。
  - 异常信息。
- 日志保存在本地文件，可在应用内查看。

---

## 9. 权限与配置

### Android

必须配置：

- 前台服务权限。
- 通知权限。
- 开机广播权限。
- 设置壁纸权限。
- 电池优化白名单引导。
- 自启动引导。
- 后台锁定引导。

### Windows

- 开机自启配置。
- 托盘常驻。
- 桌面壁纸设置权限。
- 锁屏壁纸 API 可用性检测。

---

## 10. 验收标准

完成后应满足：

1. 可以选择多个壁纸类别。
2. 至少选择 1 个类别才能保存。
3. 默认类别只能取消选择，不能修改或删除。
4. 重置后恢复默认类别。
5. 自定义类别可添加、删除。
6. 自定义类别不超过 5 个字符。
7. 所有类别总数 1 到 9 个。
8. 类别不允许重复或包含关系重复。
9. 可以立即切换壁纸。
10. 可以按 6 小时 / 12 小时 / 24 小时 / 一星期 / 永久 设置自动切换。
11. 永久模式下不自动切换。
12. 网络失败时保持当前壁纸。
13. Android 可设置桌面和锁屏壁纸。
14. Windows 可设置桌面壁纸。
15. Android 支持开机自启和前台服务常驻。
16. Windows 支持托盘常驻和开机自启。
17. 上滑一键清理后台后，Android 端在用户正确授权后仍尽量保持运行。
18. 无用户登录。
19. 图片等比例适配屏幕，不变形。
20. 项目可编译、可运行、有 README。

---

## 11. 输出要求

请输出：

1. 完整 .NET MAUI 解决方案。
2. 项目结构建议：
   - `DailyWallpaper.sln`
   - `DailyWallpaper/`
     - `Models/`
     - `Services/`
     - `ViewModels/`
     - `Views/`
     - `Platforms/Android/`
     - `Platforms/Windows/`
     - `Resources/`
3. Android 平台特定代码：
   - 前台服务。
   - 开机广播。
   - 壁纸设置。
   - 权限引导。
4. Windows 平台特定代码：
   - 托盘。
   - 开机自启。
   - 桌面壁纸设置。
   - 锁屏壁纸降级处理。
5. `README.md`：
   - 构建方式。
   - Android 权限配置步骤。
   - Windows 启动和开机自启说明。
   - 小米 HyperOS 特殊设置说明。
6. 关键代码注释。
7. 不要省略平台特定实现。

---

## 12. 备注

- “无法被关闭”在 Android 非 Root 设备上无法 100% 保证，请实现为“极难被系统杀死”，并向用户说明需要手动配置权限。
- 壁纸版权请使用公开、免费、允许个人使用的来源。
- 如果使用需要 API Key 的壁纸源，必须做成可配置，并提供无需 Key 的默认源。
- 所有设置保存在本地，不涉及用户隐私上传。