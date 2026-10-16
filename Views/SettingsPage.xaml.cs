using DailyWallpaper.ViewModels;

namespace DailyWallpaper.Views;

/// <summary>设置页：开机自启、后台常驻、壁纸选项、文件路径、关于与手动退出。</summary>
public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _viewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"刷新设置页失败：{ex}");
        }
    }
}
