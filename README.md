# DailyWallpaper 每日壁纸

跨平台（**Android / Windows 11**）的 .NET MAUI 桌面 + 锁屏壁纸自动更换应用。

按用户选定的壁纸类别，从**无需密钥的公开图片源**随机获取高清图片，使用 SkiaSharp 按目标屏幕分辨率等比缩放并居中裁剪，然后自动设置为手机（桌面 + 锁屏）或电脑（桌面，锁屏尽力而为）壁纸。

> 本项目**没有**用户登录、账号系统与云同步，所有设置仅保存在本机。
>
> **免责声明**：在非 Root 的 Android 设备上，**无法 100% 保证应用绝对不会被系统或用户杀掉**。本项目通过前台服务 + 常驻通知 + `START_STICKY` + 开机自启 + `AlarmManager` + 权限引导 的组合策略，把被杀死的概率降到很低，但极端情况下（系统内存回收、厂商策略更新、用户手动强停）仍可能被终止。Windows 端同理，只有用户从托盘菜单选择“退出”才会真正结束进程。

---

## 目录

- [1. 功能一览](#1-功能一览)
- [2. 环境要求与构建](#2-环境要求与构建)
- [3. 项目结构](#3-项目结构)
- [4. 架构说明](#4-架构说明)
- [5. 数据文件位置](#5-数据文件位置)
- [6. 壁纸类别规则（重要）](#6-壁纸类别规则重要)
- [7. 壁纸源与失败处理](#7-壁纸源与失败处理)
- [8. Android 保活与权限引导](#8-android-保活与权限引导)
- [9. Windows 托盘 / 开机自启 / 锁屏](#9-windows-托盘--开机自启--锁屏)
- [10. 已知限制](#10-已知限制)
- [11. 验证结果](#11-验证结果)

---

## 1. 功能一览

| 需求 | 实现位置 |
|---|---|
| 多类别随机获取壁纸，默认 9 个预设类别 | `Services/CategoryService.cs`、`Resources/Raw/categories.json` |
| 自定义类别（≤5 字符、禁止重复与包含关系重复、1~9 个） | `Services/CategoryNameValidator.cs`、`ViewModels/CategoriesViewModel.cs` |
| `IWallpaperProvider` 抽象 + 多源降级 | `Services/Providers/` |
| SkiaSharp 等比缩放 + 居中裁剪 | `Services/SkiaImageProcessor.cs` |
| Android 桌面 + 锁屏壁纸 | `Platforms/Android/AndroidWallpaperSetter.cs` |
| Windows 桌面壁纸（+ 锁屏尽力而为） | `Platforms/Windows/WindowsWallpaperSetter.cs` |
| 切换间隔 6h / 12h / 24h / 1 周 / 永久 | `Models/SwitchInterval.cs`、`Views/IntervalPage.xaml` |
| “立即切换”按钮（忽略定时） | `ViewModels/HomeViewModel.cs` |
| 网络失败保持当前壁纸、跳过本次、等待下一个时间点 | `Services/WallpaperRotationService.cs` |
| 本地缓存最近若干张壁纸，断网时可用 | `Services/WallpaperCacheService.cs` |
| Android 前台服务 + 常驻通知 + 开机自启 + 任务移除后重启 | `Platforms/Android/WallpaperForegroundService.cs`、`WallpaperBootReceiver.cs` |
| Android 权限引导页（小米 HyperOS 专属步骤） | `Platforms/Android/AndroidPermissionGuideService.cs` |
| Windows 托盘常驻、关闭窗口最小化到托盘、开机自启 | `Platforms/Windows/WindowsAppIntegration.cs`、`WindowsStartupService.cs` |
| 日志页（成功 / 失败 / 网络异常 / 权限异常） | `Services/LogService.cs`、`Views/LogsPage.xaml` |

界面共 6 个页面：**主页**、**类别**、**切换时间**、**日志**、**设置**（5 个底部选项卡）+ **权限引导**（Android 专属，从设置页进入）。

---

## 2. 环境要求与构建

### 2.0 通用要求

- .NET SDK **10.0**（本项目在 10.0.402 上验证通过）
- .NET MAUI 工作负载：
  ```powershell
  dotnet workload install maui
  # 如只需要这两个平台：
  dotnet workload install maui-android maui-windows
  ```
- Windows 端需要 **Windows 11 / Windows 10 1809 (10.0.17763) 及以上**，并安装 Windows App SDK 运行时（Visual Studio 或 `dotnet build` 会自动还原依赖）。

### 2.1 构建 / 运行 Windows 版本

```powershell
cd E:\Projects\DailyWallpaper\DailyWallpaper

# 构建
dotnet build -f net10.0-windows10.0.19041.0

# 运行
dotnet run -f net10.0-windows10.0.19041.0
# 或直接运行产物
.\bin\Debug\net10.0-windows10.0.19041.0\win-x64\DailyWallpaper.exe
```

Windows 端以 **非打包（unpackaged）** 方式运行（`<WindowsPackageType>None</WindowsPackageType>`），这样才可以直接写注册表 `Run` 键实现开机自启、并使用 `Shell_NotifyIcon` 托盘图标。

### 2.2 构建 / 运行 Android 版本

Android 构建需要 **Android SDK** 与 **JDK 17**。如果 `dotnet build` 报 `error XA5300: Could not find android.jar / Android SDK directory`，请显式传入路径：

```powershell
# 找到 Visual Studio 自带的 Android SDK 路径
Get-ChildItem HKCU:\Software\Xamarin\VisualStudio -ErrorAction SilentlyContinue |
	ForEach-Object { (Get-ItemProperty $_.PSPath).AndroidSdkDirectory }
# 例如：D:\Program Data\Microsoft Visual Studio\Shared\Android\android-sdk

# 如本机没有 JDK，可下载 Microsoft OpenJDK 17 并解压到可写目录
curl.exe -sL -o "$env:TEMP\jdk17.zip" https://aka.ms/download-jdk/microsoft-jdk-17.0.13-windows-x64.zip
tar -xf "$env:TEMP\jdk17.zip" -C "$env:LOCALAPPDATA\Microsoft"

# 构建（把两个路径换成你机器上的实际值）
dotnet build -f net10.0-android `
  -p:AndroidSdkDirectory="D:\Program Data\Microsoft Visual Studio\Shared\Android\android-sdk" `
  -p:JavaSdkDirectory="$env:LOCALAPPDATA\Microsoft\jdk-17.0.13+11"

# 生成 APK 并安装到已连接的设备 / 模拟器
dotnet build -t:Run -f net10.0-android `
  -p:AndroidSdkDirectory="D:\Program Data\Microsoft Visual Studio\Shared\Android\android-sdk" `
  -p:JavaSdkDirectory="$env:LOCALAPPDATA\Microsoft\jdk-17.0.13+11"
# APK 输出：bin\Debug\net10.0-android\com.dailywallpaper.app-Signed.apk
```

> 也可以把这两个路径写进 `Directory.Build.props` 或环境变量 `ANDROID_HOME` / `JAVA_HOME`，这样命令行就不用每次传参。

Android 关键配置：

| 项 | 值 |
|---|---|
| 包名 | `com.dailywallpaper.app` |
| 最低版本 | API 24（Android 7.0） |
| 目标框架 | `net10.0-android`（默认编译最新的 API 36） |

### 2.3 常见构建问题

| 现象 | 原因与处理 |
|---|---|
| `error XA5300` | 未找到 Android SDK / JDK，按 2.2 显式传 `-p:AndroidSdkDirectory` 与 `-p:JavaSdkDirectory` |
| `XA5207 / XA5302` 之类签名或 SDK 许可错误 | 在 Android SDK 目录执行 `cmdline-tools\latest\bin\sdkmanager.bat --licenses` 接受许可 |
| `MSB3021` 文件被占用 | 应用仍在运行：先结束 `DailyWallpaper.exe` / 关闭正在调试的模拟器 |
| 中文乱码、编译报“缺少标识符” | 源码含中文时必须保存为 **UTF-8 with BOM**（否则编译器按 GBK/936 解析） |
| `WMC1509 No LocalAssembly parameter` | 偶发的 XAML 编译提示，成功那次构建即可忽略 |

---

## 3. 项目结构

```
DailyWallpaper/
├─ Prompt.md                     # 需求文档（本项目的实现依据）
├─ DailyWallpaper.csproj         # 目标框架、包引用、平台配置
├─ MauiProgram.cs                # 依赖注入容器 + 服务注册（Android/Windows 条件注册）
├─ App.xaml(.cs)                 # 应用入口：加载设置、启动调度器、Windows 托盘挂钩
├─ AppShell.xaml(.cs)            # 5 个选项卡 + 权限引导页路由
├─ Models/                       # 数据模型与规则常量
│  ├─ AppSettings.cs             # 全部本地设置（JSON 序列化）
│  ├─ CategoryModels.cs          # 类别项 / 类别配置 / 校验结果
│  ├─ Defaults.cs                # 类别数量、名称长度等硬性规则
│  ├─ SwitchInterval.cs          # 6h/12h/24h/1 周/永久 + 显示名与换算
│  └─ WallpaperCacheEntry.cs / WallpaperCandidate.cs / WallpaperLogEntry.cs
├─ Services/                     # 与平台无关的核心服务
│  ├─ ISettingsService / SettingsService
│  ├─ ILogService / LogService           # JSONL 日志 + 内存最近记录 + 事件
│  ├─ ICategoryService / CategoryService # 默认/自定义类别的加载、校验、保存
│  ├─ CategoryNameValidator             # 重复与包含关系判定
│  ├─ IImageProcessor / SkiaImageProcessor
│  ├─ IWallpaperDownloader / WallpaperDownloader
│  ├─ IWallpaperCache / WallpaperCacheService
│  ├─ IWallpaperRotationService / WallpaperRotationService  # 一次完整切换流程
│  ├─ IWallpaperScheduler / WallpaperScheduler              # 定时 / 闹钟 / 到期执行
│  ├─ WallpaperHttpClients、AppJsonContext                  # HttpClient 名称与源生成 JSON
│  ├─ Platform/                  # 平台能力抽象接口
│  │  ├─ IWallpaperSetter.cs         # 设置桌面 / 锁屏壁纸
│  │  ├─ IBackgroundScheduler.cs     # 前台服务、接收器编排、重启
│  │  ├─ IStartupService.cs          # 开机自启开关
│  │  ├─ IDeviceDisplayService.cs    # 目标屏幕分辨率 + 锁屏能力
│  │  └─ IPermissionGuideService.cs  # 权限引导项
│  └─ Providers/                 # IWallpaperProvider 实现（可替换/扩展）
│     ├─ OspyoWallpaperProvider.cs        # wallpaper.ospyo.com 随机壁纸（首选）
│     ├─ UapisRandomImageProvider.cs      # uapis.cn 随机图片（兜底）
│     ├─ ImageSourceResolver.cs           # 解析 302 跳转后的真实图片地址
│     └─ KeywordCategoryMatcher.cs        # 关键词 → 图源类别参数映射
├─ Platforms/
│  ├─ Android/                   # 前台服务、开机广播、闹钟、WallpaperManager、权限引导
│  ├─ Windows/                   # 托盘、Win32 壁纸、注册表自启、锁屏 WinRT、定时器
│  ├─ iOS/ MacCatalyst/          # 模板保留，未在本项目目标框架内（不参与编译）
├─ ViewModels/                   # 6 个页面的 MVVM 逻辑（CommunityToolkit.Mvvm）
├─ Views/                        # 6 个 XAML 页面（编译期绑定 x:DataType）
└─ Resources/Raw/categories.json # 默认类别与关键词配置（首次运行释放到数据目录）
```

---

## 4. 架构说明

- **MVVM**：`CommunityToolkit.Mvvm` 的 `ObservableObject` / `[ObservableProperty]` / `[RelayCommand]` 源生成器；页面通过构造函数注入对应的 ViewModel（页面与 AppShell 注册为单例，ViewModel 为瞬态）。
- **依赖注入**：全部服务在 `MauiProgram.CreateMauiApp()` 中注册，`HttpClient` 通过 `IHttpClientFactory` 命名客户端（`wallpaper-api` 25s 超时、`download` 90s 超时）统一配置。
- **平台特定实现**：核心服务只依赖 `Services/Platform/*` 接口，Android / Windows 各自提供实现，并用 `#if ANDROID` / `#elif WINDOWS` 在 DI 中条件注册。
- **Android 组件无构造函数注入**：`Service`、`BroadcastReceiver` 由系统实例化，因此它们通过静态的 `MauiProgram.Services` 解析依赖（`MainApplication` 启动时赋值）。
- **定时**：
  - Windows：`WindowsBackgroundScheduler` 内部定时器（`System.Threading.Timer`）到点触发切换。
  - Android：前台服务内的定时器 + `AlarmManager` 精确闹钟双保险（未授权精确闹钟时退化为非精确闹钟）。
  - 应用重启 / 开机后按“上次切换时间 + 间隔”重算下次切换时间，错过的时间点一次性顺延到未来，**不会连续补切**。
- **JSON**：`AppJsonContext` 使用 System.Text.Json 源生成器，避免反射与 AOT 裁剪问题；日志为 JSONL（每行一个 JSON 对象）。

---

## 5. 数据文件位置

所有文件都在 `FileSystem.AppDataDirectory` 下：

| 文件 / 目录 | 说明 |
|---|---|
| `settings.json` | 全部用户设置（类别、间隔、上次/下次切换时间、自启等） |
| `categories.json` | 默认类别与搜索关键词配置（首次运行从安装包释放） |
| `logs/wallpaper.jsonl` | 运行日志（JSONL，每行一条） |
| `wallpapers/`、`wallpapers/index.json` | 最近若干张壁纸缓存（默认 5 张，可在设置页调整） |
| `current/current.jpg` | 当前壁纸（Windows 的 `SystemParametersInfo` 要求固定路径） |

实际路径示例：

- Windows（非打包）：`%LOCALAPPDATA%\<发布者>\com.dailywallpaper.app\Data\`
  （本机实测：`C:\Users\<你>\AppData\Local\User Name\com.dailywallpaper.app\Data`）
- Android：`/data/data/com.dailywallpaper.app/files/`（需 Root 才能用文件管理器查看；应用内“设置 → 数据目录”可复制路径）

---

## 6. 壁纸类别规则（重要）

默认预设类别固定为 9 个，**只能取消勾选，不能修改或删除**，点“重置壁纸类别选项”会恢复全部勾选：

| 类别 | 搜索关键词 |
|---|---|
| 动漫 | `anime` |
| 动物 | `animals` |
| 宇宙 | `space` |
| 星空 | `starry sky` |
| 风景 | `landscape` |
| AI | `ai art` |
| 二次元 | `anime` |
| 插画 | `illustration` |
| 赛博朋克 | `cyberpunk` |

自定义类别规则：

- 名称长度 **≤ 5 个字符**；
- 新增时同时与默认类别、已有自定义类别比较，**完全相同**或存在**包含关系**都算重复：
  - `动漫` 与 `二次元` → 不重复（可同时存在）
  - `动漫` 与 `日本动漫` → 重复（拒绝）
  - `AI` 与 `AI艺术` → 重复（拒绝）
- 自定义类别可以删除；
- 无法自动映射关键词时，直接以用户输入的名称作为搜索关键词。

### 关键词 → 各图片源类别参数的映射

网络请求用的不是中文类别名，而是上表里的 `keyword`；`KeywordCategoryMatcher` 再把 `keyword` 映射成图源支持的参数：

| 关键词 | Ospyo（`/api/v2/<type>`） | Uapi（`?category=`） |
|---|---|---|
| `anime` | `anime` | `anime` |
| `animals` | `nature` | 不传（全局随机） |
| `space` | `nature` | 不传（全局随机） |
| `starry sky` | `nature` | 不传（全局随机） |
| `landscape` | `nature` | `landscape` |
| `ai art` | 不传（全局随机） | `aidrawing`（未上线时自动退化为全局随机） |
| `illustration` | `anime` | `acg` |
| `cyberpunk` | `anime` | `acg` |

匹配规则：拉丁文关键词按**整词**匹配（避免 `ai` 误命中 `rain`），中文关键词按**包含**匹配（如“日本动漫”命中“动漫”）；两者都会先转小写、再按上表顺序取第一个命中项。映射不到时直接省掉类别参数（即“全局随机”）。想自定义映射，只需修改两个 Provider 里的 `CategoryMap` 表。

### 关于“类别总数 1~9 个”的解释

需求文档 3.1.1 要求默认预设最多 9 个（本项目取满 9 个），3.1.3 又要求“所有类别总数 1 到 9 个”。由于默认类别不允许删除，若把 9 个默认类别与自定义类别合并计入总数，自定义类别将永远无法添加，与“用户可动态添加/减少自定义类别”相矛盾。因此本项目采用如下解释（代码注释见 `Models/Defaults.cs`）：

1. **被勾选（启用）的类别数量必须在 1~9 个之间** —— 硬性约束，少于 1 个不允许保存；
2. **自定义类别最多 9 个** —— 避免列表无限膨胀；
3. 默认类别始终保留在列表中，仅可取消勾选。

### 编辑 `categories.json` 增改类别 / 关键词

`Resources/Raw/categories.json` 会在**首次运行**时被复制到数据目录，之后应用只读取数据目录中的副本：

```json
{
  "defaultCategories": [
	{ "name": "动漫", "keyword": "anime" },
	{ "name": "动物", "keyword": "animals" }
  ]
}
```

- 修改**已安装的应用**：直接编辑数据目录下的 `categories.json`（见第 5 节），然后重启应用；
- 修改**打包默认值**：编辑 `Resources/Raw/categories.json` 后重新构建；
- `keyword` 留空时以 `name` 作为搜索关键词；文件缺失或解析失败时会退化为代码内置的默认列表并在日志中记录警告。

---

## 7. 壁纸源与失败处理

`IWallpaperProvider` 抽象，默认按以下顺序**自动降级**（两者都无需申请密钥，且在中国大陆可直连）：

1. `OspyoWallpaperProvider` —— `https://wallpaper.ospyo.com/api/v2[/<type>]`，`<type>` 取 `anime` / `game` / `nature`，匿名访问、无需 Key；
2. `UapisRandomImageProvider` —— `https://uapis.cn/api/v1/random/image[?category=<类别>]`，`category` 取 `anime` / `acg` / `landscape` / `bq` / `furry`。

两者的接口都只返回 **302 跳转**，`ImageSourceResolver` 会跟随重定向拿到真实图片地址后再交给下载器，因此不会重复下载整张图片。
类别 → 接口参数的映射由 `KeywordCategoryMatcher` 完成；映射不到的关键词会退化为“全局随机”，类别本身不存在（例如 Uapi 未上线的类别返回 `404`）时同样退化为全局随机，不会因此取不到图片。

> 为什么替换掉了旧的 `WallhavenProvider` / `LoremFlickrProvider` / `PicsumProvider`：这三个源在中国大陆需要代理才能访问，`wallhaven.cc` 更是完全无法直连，导致自动切换长期失败。

完整切换流程（`WallpaperRotationService`）：

1. 从用户**勾选的类别中随机挑一个**；
2. 依次尝试各图片源，随机取一张图片；
3. `WallpaperDownloader` 下载原图；
4. `SkiaImageProcessor`：`scale = max(目标宽/图宽, 目标高/图高)` → 等比放大到覆盖屏幕 → 居中裁剪 → 输出 JPEG（避免拉伸变形）；
5. `IWallpaperSetter` 设置壁纸（Android：桌面 `FLAG_SYSTEM` + 锁屏 `FLAG_LOCK`；Windows：`SPI_SETDESKWALLPAPER`，锁屏尽力而为）；
6. 写入缓存（默认保留 5 张，尽量避开最近用过的图片）、更新 `LastSwitchTime` / `NextSwitchTime`、写日志。

**取不到图片时（含断网、图源不可用、两个源都失败）**：按需求**静默处理** —— 保持当前壁纸完全不变，只把 `NextSwitchTime` 顺延到下一个预设时间点，**不弹出任何警告或错误提示**，状态栏只显示中性的“本次未能获取新壁纸，已保持当前壁纸不变。”；日志里也只记一条 `Info`（失败原因写在明细字段里，便于排查，但不会被日志页的“警告 / 错误”筛选项命中）。缓存中的壁纸可通过主页“使用缓存壁纸”按钮手动使用。

---

## 8. Android 保活与权限引导

权限（`Platforms/Android/AndroidManifest.xml`）：`INTERNET`、`ACCESS_NETWORK_STATE`、`SET_WALLPAPER`、`RECEIVE_BOOT_COMPLETED`、`FOREGROUND_SERVICE`、`FOREGROUND_SERVICE_SPECIAL_USE`、`WAKE_LOCK`、`POST_NOTIFICATIONS`、`SCHEDULE_EXACT_ALARM`、`REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`。

保活组合策略：

- **前台服务**（`WallpaperForegroundService`）显示常驻通知，`OnStartCommand` 返回 `START_STICKY`；
- 监听 `BOOT_COMPLETED` 开机自启（Android 14+ 的前台服务类型声明为 `specialUse`）；
- `onTaskRemoved` 时尝试重新拉起服务；
- 服务内定时器 + `AlarmManager` 双重触发到点切换（Android 12+ 未授权精确闹钟时自动退化为非精确闹钟）；
- 引导用户完成以下系统设置（应用内“设置 → 权限引导”，小米 HyperOS 路径）：

| 引导项 | HyperOS 操作路径 |
|---|---|
| 允许通知 | 弹窗授权（Android 13+） |
| 允许精确闹钟 | 应用详情 → 闹钟和提醒 → 允许 |
| 关闭电池优化 | 系统设置 → 电池 → 应用省电策略 → 加入白名单 |
| 省电策略“无限制” | 设置 → 应用设置 → 应用管理 → 本应用 → 省电策略 → 无限制 |
| 开启自启动 | 设置 → 应用设置 → 应用管理 → 本应用 → 自启动 |
| 最近任务中锁定 | 打开最近任务列表，长按本应用卡片 → 锁定 |

> ⚠️ 如上设置后应用已经“极难被杀死”，但**无法保证 100% 不被关闭**：系统内存压力、厂商策略、用户手动“强行停止”都可能终止应用。应用内权限引导页同样显示了这一免责声明。

---

## 9. Windows 托盘 / 开机自启 / 锁屏

- 启动后创建**系统托盘图标**（右键菜单）：显示主界面 / 立即切换 / 退出应用；
- **关闭主窗口 = 最小化到托盘**（可在设置页关闭该行为），只有托盘菜单的“退出应用”才会真正结束进程；
- **开机自启**：写入注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`（设置页可开关，开始菜单搜索“启动应用”可查看状态）；
- **桌面壁纸**：`SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE)`；
- **锁屏壁纸**：尝试 WinRT `Windows.System.UserProfile.LockScreen.SetImageFileAsync`；若系统限制或文件不可访问则自动降级、记录日志并在主页“锁屏壁纸”一栏提示，不影响桌面壁纸切换；
- 定时由应用内的定时器驱动，因此**需要应用保持运行**（关闭窗口到托盘即可）。

---

## 10. 已知限制

1. **无法保证后台绝对不被杀死**（见第 8 节免责声明）；
2. Android 端 `SCHEDULE_EXACT_ALARM` 在部分系统上默认关闭，未授权时切换时间可能被推迟几分钟到几小时；
3. iOS / MacCatalyst 目录是 .NET MAUI 模板残留，本项目**未适配**这两个平台（`TargetFrameworks` 只包含 android 与 windows）；
4. Windows 锁屏壁纸受系统 API 限制，可能设置失败（会降级并提示）；
5. 免费图片源的可用性与图片质量由其服务方决定，网络异常时按第 7 节策略处理；
6. 修改 `categories.json` 后需要重启应用才会重新读取。

---

## 11. 验证结果

| 项目 | 结果 |
|---|---|
| `dotnet build -f net10.0-windows10.0.19041.0` | ✅ 成功，**0 警告 / 0 错误** |
| `dotnet build -f net10.0-android`（显式 SDK/JDK 路径） | ✅ 成功，**0 警告 / 0 错误** |
| Windows 端运行 | ✅ 窗口正常显示（标题 `DailyWallpaper 每日壁纸`），`settings.json`、`logs/wallpaper.jsonl` 正常写入 |
| 托盘与关闭行为 | ✅ 日志记录“已创建系统托盘图标”；发送 `WM_CLOSE` 后进程仍在运行（关闭窗口 = 最小化到托盘） |
| Android 端 | ⚠️ 仅完成编译验证，未在真机 / 模拟器上运行（权限、前台服务、闹钟行为需实机确认） |
| 端到端切换壁纸 | ⚠️ 未主动执行（会真实修改你的桌面壁纸与联网下载图片），请在主页点“立即切换”自行确认 |
