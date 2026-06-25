using AutoPartsShop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AutoPartsShop.UI.Views.Settings;

public partial class SettingsView : System.Windows.Controls.Page
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<SettingsViewModel>();
        Loaded += SettingsView_Loaded;
    }

    private async void SettingsView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            await vm.LoadSettingsCommand.ExecuteAsync(null);
        }
    }
}
