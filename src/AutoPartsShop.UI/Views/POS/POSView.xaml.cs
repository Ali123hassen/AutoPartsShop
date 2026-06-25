using AutoPartsShop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace AutoPartsShop.UI.Views.POS
{
    public partial class POSView : Page
    {
        public POSView()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<POSViewModel>();
        }
    }
}
