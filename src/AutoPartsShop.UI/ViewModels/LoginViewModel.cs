using AutoPartsShop.Application.DTOs.Auth;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace AutoPartsShop.UI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// اسم المحل - يُحمّل من الإعدادات
    /// </summary>
    [ObservableProperty]
    private string _shopName = "قطع غيار السيارات";

    /// <summary>
    /// مسار صورة اللوقو - يُحمّل من الإعدادات
    /// </summary>
    [ObservableProperty]
    private string _shopLogoPath = string.Empty;

    /// <summary>
    /// هل يوجد لوقو؟ (لإظهار الصورة بدل النص)
    /// </summary>
    [ObservableProperty]
    private bool _hasShopLogo;

    public LoginViewModel(IServiceScopeFactory scopeFactory, IServiceProvider serviceProvider)
    {
        _scopeFactory = scopeFactory;
        _serviceProvider = serviceProvider;

        // تحميل اسم المحل من الإعدادات
        _ = LoadShopNameAsync();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "يرجى إدخال اسم المستخدم وكلمة المرور";
            HasError = true;
            return;
        }

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            // Create a NEW scope → fresh DbContext for this login operation
            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

            var loginDto = new LoginDto { Username = Username, Password = Password };
            var result = await authService.LoginAsync(loginDto);

            if (result.IsSuccess && result.Value != null)
            {
                // Open MainWindow
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();

                // Close LoginView
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window is Views.LoginView)
                    {
                        window.Close();
                        break;
                    }
                }
            }
            else
            {
                ErrorMessage = result.Error ?? "فشل تسجيل الدخول";
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"حدث خطأ: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// يحمل اسم المحل من قاعدة البيانات
    /// </summary>
    private async Task LoadShopNameAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();
            var settings = await settingService.GetAllAsync();

            if (settings.TryGetValue("ShopName", out var shopName) && !string.IsNullOrWhiteSpace(shopName))
            {
                ShopName = shopName;
            }

            if (settings.TryGetValue("ShopLogoPath", out var logoPath) && !string.IsNullOrWhiteSpace(logoPath))
            {
                ShopLogoPath = logoPath;
                HasShopLogo = System.IO.File.Exists(logoPath);
            }
            else
            {
                ShopLogoPath = string.Empty;
                HasShopLogo = false;
            }
        }
        catch
        {
            // استخدم القيمة الافتراضية
        }
    }
}
