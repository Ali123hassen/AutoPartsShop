using AutoPartsShop.Application.DTOs.Auth;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;
using WinApp = System.Windows.Application;

namespace AutoPartsShop.UI.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IAuthService _authService;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationService _notificationService;
    private readonly DispatcherTimer _stockCheckTimer;

    [ObservableProperty]
    private string _currentPageTitle = "لوحة التحكم";

    [ObservableProperty]
    private UserDto? _currentUser;

    [ObservableProperty]
    private string _activeNavButton = "Dashboard";

    [ObservableProperty]
    private int _unreadNotificationCount;

    [ObservableProperty]
    private bool _hasUnreadNotifications;

    [ObservableProperty]
    private bool _isNotificationPanelOpen;

    [ObservableProperty]
    private List<NotificationItem> _notifications = [];

    /// <summary>
    /// اسم المحل - يُحمّل من الإعدادات ويتغير ديناميكياً عند التعديل
    /// </summary>
    [ObservableProperty]
    private string _shopName = "قطع غيار السيارات";

    /// <summary>
    /// مسار صورة اللوقو - يُحمّل من الإعدادات ويتغير ديناميكياً عند التعديل
    /// </summary>
    [ObservableProperty]
    private string _shopLogoPath = string.Empty;

    /// <summary>
    /// هل يوجد لوقو؟ (لإظهار الصورة بدل النص)
    /// </summary>
    [ObservableProperty]
    private bool _hasShopLogo;

    // مراجع معالجات الأحداث لإلغاء الاشتراك لاحقاً
    private readonly Action<int> _unreadCountHandler;
    private readonly Action<NotificationItem> _toastHandler;

    private bool _disposed = false;

    public MainViewModel(IAuthService authService, IServiceProvider serviceProvider)
    {
        _authService = authService;
        _serviceProvider = serviceProvider;
        _currentUser = authService.CurrentUser;

        // تحميل اسم المحل واللوقو من الإعدادات
        _ = LoadShopNameAsync();

        // Get notification service (singleton)
        _notificationService = serviceProvider.GetRequiredService<INotificationService>();

        // إنشاء معالجات الأحداث كحقول حتى نتمكن من إلغاء الاشتراك
        _unreadCountHandler = count =>
        {
            WinApp.Current.Dispatcher.Invoke(() =>
            {
                UnreadNotificationCount = count;
                HasUnreadNotifications = count > 0;
            });
        };

        _toastHandler = item =>
        {
            // Toast display is handled by the MainWindow code-behind
        };

        // Subscribe to notification events
        _notificationService.UnreadCountChanged += _unreadCountHandler;
        _notificationService.ToastRequested += _toastHandler;

        // Setup periodic low stock check (every 5 minutes)
        _stockCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        _stockCheckTimer.Tick += async (s, e) => await _notificationService.CheckLowStockAsync();
        _stockCheckTimer.Start();

        // Run initial check after a short delay
        _ = Task.Delay(3000).ContinueWith(_ =>
        {
            WinApp.Current.Dispatcher.Invoke(async () =>
            {
                await _notificationService.CheckLowStockAsync();
            });
        });
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        CurrentPageTitle = "لوحة التحكم";
        ActiveNavButton = "Dashboard";
        NavigateToPage<Views.DashboardView>();
    }

    [RelayCommand]
    private void NavigateToSpareParts()
    {
        CurrentPageTitle = "إدارة قطع الغيار";
        ActiveNavButton = "SpareParts";
        NavigateToPage<Views.SpareParts.SparePartListView>();
    }

    [RelayCommand]
    private void NavigateToPOS()
    {
        CurrentPageTitle = "نقطة البيع";
        ActiveNavButton = "POS";
        NavigateToPage<Views.POS.POSView>();
    }

    [RelayCommand]
    private void NavigateToPurchaseInvoice()
    {
        CurrentPageTitle = "فاتورة المشتريات";
        ActiveNavButton = "PurchaseInvoice";
        NavigateToPage<Views.PurchaseInvoices.PurchaseInvoiceView>();
    }

    [RelayCommand]
    private void NavigateToInvoices()
    {
        CurrentPageTitle = "الفواتير";
        ActiveNavButton = "Invoices";
        NavigateToPage<Views.Invoices.InvoiceListView>();
    }

    [RelayCommand]
    private void NavigateToReturns()
    {
        CurrentPageTitle = "المرتجعات والاستبدال";
        ActiveNavButton = "Returns";
        NavigateToPage<Views.Returns.ReturnView>();
    }

    [RelayCommand]
    private void NavigateToInventory()
    {
        CurrentPageTitle = "إدارة المخزون";
        ActiveNavButton = "Inventory";
        NavigateToPage<Views.Inventory.StockMovementView>();
    }

    [RelayCommand]
    private void NavigateToReports()
    {
        CurrentPageTitle = "التقارير";
        ActiveNavButton = "Reports";
        NavigateToPage<Views.Reports.ReportsView>();
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentPageTitle = "الإعدادات";
        ActiveNavButton = "Settings";
        NavigateToPage<Views.Settings.SettingsView>();
    }

    [RelayCommand]
    private void NavigateToUserManagement()
    {
        CurrentPageTitle = "إدارة المستخدمين";
        ActiveNavButton = "UserManagement";
        NavigateToPage<Views.Settings.UserManagementView>();
    }

    [RelayCommand]
    private void ToggleNotificationPanel()
    {
        IsNotificationPanelOpen = !IsNotificationPanelOpen;

        if (IsNotificationPanelOpen)
        {
            Notifications = _notificationService.GetUnreadNotifications();
        }
    }

    [RelayCommand]
    private void MarkNotificationsRead()
    {
        _notificationService.MarkAllAsRead();
        Notifications = [];
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        // إيقاف المؤقت وإلغاء الاشتراك في الأحداث لمنع تسرب الذاكرة
        Cleanup();

        await _authService.LogoutAsync();

        var loginView = _serviceProvider.GetRequiredService<LoginView>();
        loginView.Show();

        foreach (Window window in WinApp.Current.Windows)
        {
            if (window is MainWindow)
            {
                window.Close();
                break;
            }
        }
    }

    private void NavigateToPage<T>() where T : System.Windows.Controls.Page
    {
        try
        {
            var page = _serviceProvider.GetRequiredService<T>();

            // Find the Frame in MainWindow
            foreach (Window window in WinApp.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    var frame = mainWindow.FindName("MainFrame") as System.Windows.Controls.Frame;
                    frame?.Navigate(page);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"خطأ في التنقل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// يحمل اسم المحل من قاعدة البيانات
    /// </summary>
    private async Task LoadShopNameAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
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

    /// <summary>
    /// يُستدعى من الخارج عند تغيير اسم المحل في الإعدادات لتحديث الشريط الجانبي وعنوان النافذة
    /// </summary>
    public async Task RefreshShopNameAsync()
    {
        await LoadShopNameAsync();
    }

    /// <summary>
    /// تنظيف الموارد: إيقاف المؤقت وإلغاء اشتراك الأحداث لمنع تسرب الذاكرة.
    /// يُستدعى عند تسجيل الخروج أو إغلاق النافذة.
    /// </summary>
    private void Cleanup()
    {
        if (_disposed) return;
        _disposed = true;

        // إيقاف مؤقت فحص المخزون
        _stockCheckTimer?.Stop();

        // إلغاء اشتراك الأحداث في NotificationService (Singleton)
        // بدون هذا، يحتفظ الـ Singleton بمرجع للـ ViewModel القديم ويمنع garbage collection
        if (_notificationService != null)
        {
            _notificationService.UnreadCountChanged -= _unreadCountHandler;
            _notificationService.ToastRequested -= _toastHandler;
        }
    }

    public void Dispose()
    {
        Cleanup();
    }
}
