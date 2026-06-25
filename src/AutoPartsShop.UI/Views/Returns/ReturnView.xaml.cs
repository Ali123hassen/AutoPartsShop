using AutoPartsShop.Application.DTOs.Returns;
using AutoPartsShop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoPartsShop.UI.Views.Returns
{
    public partial class ReturnView : Page
    {
        public ReturnView()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<ReturnViewModel>();
            Loaded += ReturnView_Loaded;
        }

        private async void ReturnView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ReturnViewModel vm)
            {
                await vm.LoadReturnsCommand.ExecuteAsync(null);
            }
        }

        private void ReturnsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ReturnViewModel vm && vm.SelectedReturn != null)
            {
                _ = vm.ViewReturnDetailsCommand.ExecuteAsync(null);
            }
        }

        private void InvoiceNumber_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBlock tb && !string.IsNullOrEmpty(tb.Text))
            {
                System.Windows.Clipboard.SetText(tb.Text);
                if (DataContext is ReturnViewModel vm)
                {
                    vm.ShowStatus($"تم نسخ رقم الفاتورة: {tb.Text}");
                }
            }
        }

        private void InvoiceNumberDetail_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBlock tb && !string.IsNullOrEmpty(tb.Text))
            {
                System.Windows.Clipboard.SetText(tb.Text);
                if (DataContext is ReturnViewModel vm)
                {
                    vm.ShowStatus($"تم نسخ رقم الفاتورة: {tb.Text}");
                }
            }
        }

        private void DismissStatus_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ReturnViewModel vm)
            {
                vm.DismissStatus();
            }
        }
    }
}
