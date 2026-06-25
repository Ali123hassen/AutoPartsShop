using AutoPartsShop.UI.ViewModels;
using System.Windows.Controls;

namespace AutoPartsShop.UI.Views
{
    public partial class DashboardView : Page
    {
        public DashboardView(DashboardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
