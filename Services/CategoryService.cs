using System.Text.Json;
using DailyWallpaper.Models;
using Microsoft.Maui.Storage;

namespace DailyWallpaper.Services;

/// <summary>
/// <see cref="ICategoryService"/> 的默认实现。
/// </summary>
/// <remarks>
/// 首次运行时把安装包内的 <c>Resources/Raw/categories.json</c> 复制到 <see cref="ConfigFilePath"/>，
/// 之后只读取应用数据目录中的副本，因此用户可以直接编辑该文件来增删默认类别或修改关键词。
/// </remarks>
public sealed class CategoryService : ICategoryService
{
    private const string AssetFileName = "categories.json";

    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private WallpaperCategoryConfig? _config;

    public CategoryService(ISettingsService settings, ILogService log)
    {
        _settings = settings;
        _log = log;
        ConfigFilePath = Path.Combine(FileSystem.AppDataDirectory, AssetFileName);
    }

    /// <inheritdoc />
    public string ConfigFilePath { get; }

    /// <inheritdoc />
    public async Task<WallpaperCategoryConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        if (_config is not null)
        {
            return _config;
        }

        return await ReloadConfigAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WallpaperCategoryConfig> ReloadConfigAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConfigFileAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await using var stream = File.OpenRead(ConfigFilePath);
                var config = await JsonSerializer
                    .DeserializeAsync(stream, AppJsonContext.Default.WallpaperCategoryConfig, cancellationToken)
                    .ConfigureAwait(false);

                if (config?.DefaultCategories is { Count: > 0 })
                {
                    _config = config;
                    return _config;
                }

                _log.Write(WallpaperLogLevel.Warning, "categories.json 中没有可用的默认类别，已使用内置列表", source: ConfigFilePath);
            }
            catch (Exception ex)
            {
                _log.Write(WallpaperLogLevel.Warning, "读取 categories.json 失败，已使用内置默认类别", ex, source: ConfigFilePath);
            }

            _config = BuildFallbackConfig();
            return _config;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryItem>> BuildItemsAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync(cancellationToken).ConfigureAwait(false);
        var settings = _settings.Current;

        var selected = new HashSet<string>(settings.SelectedCategories, StringComparer.Ordinal);
        var items = new List<CategoryItem>();

        foreach (var definition in config.DefaultCategories)
        {
            items.Add(new CategoryItem(
                definition.Name,
                string.IsNullOrWhiteSpace(definition.Keyword) ? definition.Name : definition.Keyword,
                isDefault: true,
                isSelected: selected.Contains(definition.Name)));
        }

        foreach (var name in settings.CustomCategories)
        {
            items.Add(new CategoryItem(
                name,
                config.FindKeyword(name) ?? name,
                isDefault: false,
                isSelected: selected.Contains(name)));
        }

        return items;
    }

    /// <inheritdoc />
    public async Task<string> ResolveKeywordAsync(string categoryName, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync(cancellationToken).ConfigureAwait(false);
        var keyword = config.FindKeyword(categoryName);
        return string.IsNullOrWhiteSpace(keyword) ? categoryName : keyword;
    }

    /// <inheritdoc />
    public CategoryValidationResult ValidateNewCategoryName(string? input, IEnumerable<string> existingNames) =>
        CategoryNameValidator.Validate(input, existingNames);

    /// <inheritdoc />
    public async Task<CategoryValidationResult> AddCustomCategoryAsync(
        string? input,
        IEnumerable<string> existingNames,
        CancellationToken cancellationToken = default)
    {
        var validation = CategoryNameValidator.Validate(input, existingNames);
        if (!validation.IsValid)
        {
            return validation;
        }

        var settings = _settings.Current;
        if (settings.CustomCategories.Count >= Defaults.MaxCustomCategories)
        {
            return CategoryValidationResult.Fail($"自定义类别最多 {Defaults.MaxCustomCategories} 个，请先删除部分类别。");
        }

        var name = input!.Trim();
        settings.CustomCategories.Add(name);

        // 新添加的类别默认勾选，但总数不能超过上限。
        if (settings.SelectedCategories.Count < Defaults.MaxSelectedCategories
            && !settings.SelectedCategories.Contains(name, StringComparer.Ordinal))
        {
            settings.SelectedCategories.Add(name);
        }

        await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        _log.Write(WallpaperLogLevel.Info, $"已添加自定义类别：{name}", category: name);

        return CategoryValidationResult.Ok;
    }

    /// <inheritdoc />
    public async Task DeleteCustomCategoryAsync(string name, CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;
        var removed = settings.CustomCategories.RemoveAll(item => string.Equals(item, name, StringComparison.Ordinal));
        settings.SelectedCategories.RemoveAll(item => string.Equals(item, name, StringComparison.Ordinal));

        if (removed > 0)
        {
            await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            _log.Write(WallpaperLogLevel.Info, $"已删除自定义类别：{name}", category: name);
        }
    }

    /// <inheritdoc />
    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync(cancellationToken).ConfigureAwait(false);
        var settings = _settings.Current;

        settings.CustomCategories.Clear();
        settings.SelectedCategories = config.DefaultCategories
            .Select(item => item.Name)
            .Take(Defaults.MaxSelectedCategories)
            .ToList();

        await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        _log.Write(WallpaperLogLevel.Info, "已重置壁纸类别为默认预设");
    }

    /// <summary>首次运行把随包分发的默认配置复制到应用数据目录。</summary>
    private async Task EnsureConfigFileAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(ConfigFilePath))
        {
            return;
        }

        try
        {
            using var asset = await FileSystem.OpenAppPackageFileAsync(AssetFileName).ConfigureAwait(false);
            var directory = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var target = File.Create(ConfigFilePath);
            await asset.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Write(WallpaperLogLevel.Warning, "释放默认 categories.json 失败，将使用内置默认类别", ex);
        }
    }

    /// <summary>安装包资源不可用时的兜底默认类别。</summary>
    private static WallpaperCategoryConfig BuildFallbackConfig() => new()
    {
        DefaultCategories = new List<CategoryDefinition>
        {
            new() { Name = "动漫", Keyword = "anime" },
            new() { Name = "动物", Keyword = "animals" },
            new() { Name = "宇宙", Keyword = "space" },
            new() { Name = "星空", Keyword = "starry sky" },
            new() { Name = "风景", Keyword = "landscape" },
            new() { Name = "AI", Keyword = "ai art" },
            new() { Name = "二次元", Keyword = "anime" },
            new() { Name = "插画", Keyword = "illustration" },
            new() { Name = "赛博朋克", Keyword = "cyberpunk" }
        }
    };
}
