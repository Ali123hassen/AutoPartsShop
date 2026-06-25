using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;

namespace AutoPartsShop.UI.Views;

public partial class MainWindow : Window
{
    private DispatcherTimer? _toastTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainViewModel>();
        Loaded += OnLoaded;

        // Subscribe to toast notifications
        var notificationService = App.Services.GetRequiredService<INotificationService>();
        notificationService.ToastRequested += ShowToast;

        // ===== مهم: إغلاق البرنامج عند إغلاق النافذة الرئيسية =====
        // لأن App.xaml يستخدم ShutdownMode="OnExplicitShutdown"، النافذة
        // لا تُغلق تلقائياً عند ضغط X. يجب استدعاء Shutdown() يدوياً.
        // بدون هذا، العملية تبقى في Task Manager.
        Closed += OnMainWindowClosed;
    }

    /// <summary>
    /// يُستدعى عند إغلاق MainWindow (ضغط X أو رمز الإغلاق).
    /// نقوم بإغلاق التطبيق بالكامل لمنع بقاء العملية في Task Manager.
    /// ملاحظة: عند تسجيل الخروج (Logout)، نفتح LoginView أولاً ثم نُغلق MainWindow،
    /// لذا System.Windows.Application.Current.Windows سيكون فيه LoginView ولن يُغلق التطبيق.
    /// 
    /// ملاحظة تقنية: نستخدم System.Windows.Application بدلاً من Application فقط
    /// لأن المشروع يحتوي على namespace باسم AutoPartsShop.Application،
    /// مما يسبب تعارضاً مع System.Windows.Application.
    /// </summary>
    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        // تحقق من وجود نوافذ أخرى مفتوحة (مثل LoginView عند تسجيل الخروج)
        // لو لم توجد نوافذ أخرى، أغلق التطبيق
        var otherWindowsOpen = false;
        foreach (var w in System.Windows.Application.Current.Windows)
        {
            if (w is LoginView or LicenseActivationView)
            {
                otherWindowsOpen = true;
                break;
            }
        }

        if (!otherWindowsOpen)
        {
            // لا توجد نوافذ أخرى → أغلق التطبيق بالكامل
            System.Windows.Application.Current.Shutdown();
        }
        // لو توجد LoginView (تسجيل خروج)، لا تفعل شيئاً — التطبيق سيستمر
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NavigateToDashboardCommand.Execute(null);
    }

    private void ShowToast(NotificationItem item)
    {
        Dispatcher.Invoke(() =>
        {
            ToastTitle.Text = item.Title;
            ToastMessage.Text = item.Message;
            ToastBorder.Tag = item.Type.ToString();
            ToastBorder.Visibility = Visibility.Visible;

            // Reset and start the auto-hide timer
            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            _toastTimer.Tick += (s, e) =>
            {
                ToastBorder.Visibility = Visibility.Collapsed;
                _toastTimer.Stop();
            };
            _toastTimer.Start();
        });
    }
}
