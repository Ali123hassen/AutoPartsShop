using AutoPartsShop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace AutoPartsShop.UI.Views.Reports
{
    public partial class ReportsView : Page
    {
        public ReportsView()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<ReportsViewModel>();
        }

        private void ReportTypeCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement element && int.TryParse(element.Tag?.ToString(), out int reportType))
            {
                if (DataContext is ReportsViewModel vm)
                {
                    vm.SelectedReportType = reportType;
                }
            }
        }
    }
}
