using AutoPartsShop.Application.DTOs.Invoices;
using AutoPartsShop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace AutoPartsShop.UI.Views.Invoices
{
    public partial class InvoiceListView : Page
    {
        public InvoiceListView()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<InvoiceListViewModel>();
            Loaded += InvoiceListView_Loaded;
        }

        private async void InvoiceListView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is InvoiceListViewModel vm)
            {
                await vm.LoadInvoicesCommand.ExecuteAsync(null);
            }
        }

        private void InvoicesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is InvoiceListViewModel vm && vm.SelectedInvoice != null)
            {
                vm.ViewInvoiceDetailCommand.Execute(vm.SelectedInvoice);
            }
        }
    }
}
