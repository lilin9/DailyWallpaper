using DailyWallpaper.ViewModels;

namespace DailyWallpaper.Views;

/// <summary>切换时间设置页：6 小时 / 12 小时 / 一天 / 一星期 / 永久。</summary>
public partial class IntervalPage : ContentPage
{
    private readonly IntervalViewModel _viewModel;

    public IntervalPage(IntervalViewModel viewModel)
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
            System.Diagnostics.Debug.WriteLine($"刷新切换时间页失败：{ex}");
        }
    }
}
