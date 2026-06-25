using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace AutoPartsShop.UI.Views.SpareParts;

public partial class SparePartListView : Page
{
    private readonly SparePartListViewModel _viewModel;
    private readonly IServiceScopeFactory _scopeFactory;

    public SparePartListView()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<SparePartListViewModel>();
        _scopeFactory = App.Services.GetRequiredService<IServiceScopeFactory>();

        DataContext = _viewModel;

        _viewModel.AddNewRequested += OnAddNewRequested;
        _viewModel.EditRequested += OnEditRequested;
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            _ = _viewModel.SearchCommand.ExecuteAsync(null);
        }
    }

    private async void OnAddNewRequested()
    {
        var editView = new SparePartEditView();
        editView.Owner = System.Windows.Window.GetWindow(this);

        // Load categories before showing
        await editView.ViewModel.LoadCategoriesAsync();

        editView.Closed += (s, e) =>
        {
            _ = _viewModel.RefreshCommand.ExecuteAsync(null);
        };

        editView.Show();
    }

    private async void OnEditRequested(int id)
    {
        try
        {
            // Create a NEW scope → fresh DbContext for this operation
            using var scope = _scopeFactory.CreateScope();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();

            var sparePart = await sparePartService.GetByIdAsync(id);
            if (sparePart == null)
            {
                System.Windows.MessageBox.Show("لم يتم العثور على القطعة", "خطأ",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var editView = new SparePartEditView();
            editView.Owner = System.Windows.Window.GetWindow(this);

            // Load categories first, then load spare part data for editing
            await editView.ViewModel.LoadCategoriesAsync();
            editView.ViewModel.LoadForEdit(sparePart);

            editView.Closed += (s, e) =>
            {
                _ = _viewModel.RefreshCommand.ExecuteAsync(null);
            };

            editView.Show();
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show($"خطأ في تحميل البيانات: {ex.Message}", "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
