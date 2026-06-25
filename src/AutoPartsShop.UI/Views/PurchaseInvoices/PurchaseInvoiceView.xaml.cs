using AutoPartsShop.Application.DTOs.PurchaseInvoices;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace AutoPartsShop.UI.Views.PurchaseInvoices;

public partial class PurchaseInvoiceView : Page
{
    public PurchaseInvoiceView(PurchaseInvoiceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void SearchResults_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is AutoPartsShop.Application.DTOs.SpareParts.SparePartDto sparePart)
        {
            if (DataContext is PurchaseInvoiceViewModel vm)
            {
                vm.AddToCartCommand.Execute(sparePart);
            }
        }
    }

    private async void InvoiceList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not PurchaseInvoiceViewModel vm) return;
        if (vm.SelectedPurchaseInvoice is not PurchaseInvoiceDto selectedInvoice) return;

        try
        {
            // Load full invoice details with items
            using var scope = App.Services.CreateScope();
            var purchaseInvoiceService = scope.ServiceProvider.GetRequiredService<IPurchaseInvoiceService>();
            var fullInvoice = await purchaseInvoiceService.GetByIdAsync(selectedInvoice.Id);

            if (fullInvoice != null)
            {
                var detailWindow = new PurchaseInvoiceDetailWindow(fullInvoice);
                detailWindow.Owner = System.Windows.Window.GetWindow(this);
                detailWindow.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"خطأ في تحميل تفاصيل الفاتورة: {ex.Message}", "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
