using AutoPartsShop.UI.ViewModels;
using System.Windows.Controls;

namespace AutoPartsShop.UI.Views.Settings
{
    public partial class UserManagementView : Page
    {
        public UserManagementView(UserManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
