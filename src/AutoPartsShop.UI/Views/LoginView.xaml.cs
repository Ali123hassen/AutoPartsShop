using AutoPartsShop.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace AutoPartsShop.UI.Views;

public partial class LoginView : Window
{
    public LoginView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<LoginViewModel>();

        // Handle password binding manually since PasswordBox doesn't support binding
        PasswordBox.PasswordChanged += (s, e) =>
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.Password = PasswordBox.Password;
            }
        };

        // ===== مهم: إغلاق البرنامج عند إغلاق نافذة تسجيل الدخول =====
        // لأن App.xaml يستخدم ShutdownMode="OnExplicitShutdown"،
        // إغلاق LoginView بـ X لا يُغلق البرنامج تلقائياً.
        // بدون هذا، العملية تبقى في Task Manager.
        Closed += OnLoginViewClosed;
    }

    /// <summary>
    /// يُستدعى عند إغلاق LoginView بـ X.
    /// لو لم توجد MainWindow مفتوحة، فهذا يعني أن المستخدم يريد الخروج.
    /// 
    /// ملاحظة تقنية: نستخدم System.Windows.Application بدلاً من Application فقط
    /// لأن المشروع يحتوي على namespace باسم AutoPartsShop.Application،
    /// مما يسبب تعارضاً مع System.Windows.Application.
    /// </summary>
    private void OnLoginViewClosed(object? sender, EventArgs e)
    {
        // تحقق من وجود MainWindow مفتوحة (تسجيل دخول ناجح)
        var mainWindowOpen = false;
        foreach (var w in System.Windows.Application.Current.Windows)
        {
            if (w is MainWindow)
            {
                mainWindowOpen = true;
                break;
            }
        }

        if (!mainWindowOpen)
        {
            // لا توجد MainWindow → المستخدم يريد الخروج
            System.Windows.Application.Current.Shutdown();
        }
        // لو توجد MainWindow (تسجيل دخول ناجح)، لا تفعل شيئاً
    }
}
