using AutoPartsShop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace AutoPartsShop.UI.Views.Inventory
{
    public partial class StockMovementView : Page
    {
        private readonly StockMovementViewModel _viewModel;

        public StockMovementView()
        {
            InitializeComponent();

            _viewModel = App.Services.GetRequiredService<StockMovementViewModel>();
            DataContext = _viewModel;
        }
    }
}
