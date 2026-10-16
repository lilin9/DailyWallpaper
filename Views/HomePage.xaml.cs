using DailyWallpaper.ViewModels;

namespace DailyWallpaper.Views;

/// <summary>主页：当前壁纸预览、下次切换时间、自动切换开关、立即切换。</summary>
public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
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
            await _viewModel.RefreshAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"刷新主页失败：{ex}");
        }
    }
}
