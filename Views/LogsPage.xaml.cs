using DailyWallpaper.ViewModels;

namespace DailyWallpaper.Views;

/// <summary>日志页：切换成功 / 失败、网络异常、权限异常等记录，支持筛选与清空。</summary>
public partial class LogsPage : ContentPage
{
    private readonly LogsViewModel _viewModel;

    public LogsPage(LogsViewModel viewModel)
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
            System.Diagnostics.Debug.WriteLine($"刷新日志页失败：{ex}");
        }
    }
}
