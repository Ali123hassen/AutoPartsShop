using AutoPartsShop.UI.ViewModels;
using AutoPartsShop.UI.Views.Inventory;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace AutoPartsShop.UI.Views.SpareParts;

public partial class SparePartEditView : Window
{
    private readonly SparePartEditViewModel _viewModel;

    public SparePartEditView()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<SparePartEditViewModel>();
        DataContext = _viewModel;

        _viewModel.SaveCompleted += OnSaveCompleted;
        _viewModel.CancelRequested += OnCancelRequested;
        _viewModel.NavigateToStockRequested += OnNavigateToStockRequested;
    }

    public SparePartEditViewModel ViewModel => _viewModel;

    private void OnSaveCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            var owner = this.Owner;
            System.Windows.MessageBox.Show(this, "تم الحفظ بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
            owner?.Activate();
        });
    }

    private void OnCancelRequested()
    {
        Dispatcher.Invoke(() => Close());
    }

    /// <summary>
    /// يُغلق النافذة عند الضغط على مفتاح ESC.
    /// </summary>
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    /// <summary>
    /// إضافة سيارة جديدة عند الضغط على Enter في حقل السيارة.
    /// </summary>
    private void CompatibleCarInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (_viewModel.AddCompatibleCarCommand.CanExecute(null))
            {
                _viewModel.AddCompatibleCarCommand.Execute(null);
            }
            e.Handled = true;
        }
    }

    /// <summary>
    /// إضافة موديل جديد عند الضغط على Enter في حقل الموديل.
    /// </summary>
    private void CarModelInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (_viewModel.AddCarModelCommand.CanExecute(null))
            {
                _viewModel.AddCarModelCommand.Execute(null);
            }
            e.Handled = true;
        }
    }

    private void OnNavigateToStockRequested()
    {
        Dispatcher.Invoke(() =>
        {
            // إغلاق نافذة التعديل
            var owner = this.Owner;
            Close();

            // البحث عن نافذة MainWindow والتنقل لصفحة المخزون
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    var mainViewModel = mainWindow.DataContext as MainViewModel;
                    mainViewModel?.NavigateToInventoryCommand.Execute(null);
                    mainWindow.Activate();
                    break;
                }
            }

            owner?.Activate();
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _viewModel.SaveCompleted -= OnSaveCompleted;
        _viewModel.CancelRequested -= OnCancelRequested;
        _viewModel.NavigateToStockRequested -= OnNavigateToStockRequested;
    }
}