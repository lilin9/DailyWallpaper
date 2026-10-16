using DailyWallpaper.Models;
using DailyWallpaper.ViewModels;

namespace DailyWallpaper.Views;

/// <summary>类别设置页：默认类别勾选、自定义类别增删、重置与保存。</summary>
public partial class CategoriesPage : ContentPage
{
    private readonly CategoriesViewModel _viewModel;

    public CategoriesPage(CategoriesViewModel viewModel)
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
            System.Diagnostics.Debug.WriteLine($"刷新类别页失败：{ex}");
        }
    }

    private void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: CategoryItem item })
        {
            _viewModel.DeleteCommand.Execute(item);
        }
    }
}
