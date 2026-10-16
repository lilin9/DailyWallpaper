using DailyWallpaper.ViewModels;

namespace DailyWallpaper.Views;

/// <summary>权限引导页（Android）：自启动、电池优化、省电策略、通知、精确闹钟、后台锁定。</summary>
public partial class PermissionsPage : ContentPage
{
    private readonly PermissionsViewModel _viewModel;

    public PermissionsPage(PermissionsViewModel viewModel)
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
            System.Diagnostics.Debug.WriteLine($"刷新权限引导页失败：{ex}");
        }
    }

    private void OnActionClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: PermissionItemViewModel item })
        {
            _viewModel.ExecuteCommand.Execute(item);
        }
    }
}
