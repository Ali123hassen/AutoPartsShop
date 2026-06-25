using System.Windows;

namespace AutoPartsShop.UI.Views;

public partial class LicenseActivationView : Window
{
    public LicenseActivationView()
    {
        InitializeComponent();
    }

    public void SetCloseAction(Action closeAction)
    {
        if (DataContext is ViewModels.LicenseActivationViewModel vm)
        {
            vm.RequestClose += () =>
            {
                this.DialogResult = true;
                closeAction();
            };
        }
    }
}
