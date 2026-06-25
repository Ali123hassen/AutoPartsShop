using AutoPartsShop.Application;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Core.Enums;
using AutoPartsShop.Infrastructure;
using AutoPartsShop.Infrastructure.Persistence;
using AutoPartsShop.UI.Services;
using AutoPartsShop.UI.ViewModels;
using AutoPartsShop.UI.Views;
using AutoPartsShop.UI.Views.SpareParts;
using AutoPartsShop.UI.Views.POS;
using AutoPartsShop.UI.Views.Invoices;
using AutoPartsShop.UI.Views.Returns;
using AutoPartsShop.UI.Views.Inventory;
using AutoPartsShop.UI.Views.Reports;
using AutoPartsShop.UI.Views.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace AutoPartsShop.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateDefaultBuilder();

        builder.ConfigureServices((context, services) =>
        {
            ApplicationServiceExtensions.AddApplication(services);
            InfrastructureServiceExtensions.AddInfrastructure(services,
                context.Configuration.GetConnectionString("DefaultConnection")
                ?? "Server=.\\SQLEXPRESS;Database=AutoPartsShopDb;Trusted_Connection=True;TrustServerCertificate=True;");

            // License Services
            services.AddSingleton<IHardwareFingerprintService, Services.HardwareFingerprintService>();
            services.AddSingleton<ILicenseService, AutoPartsShop.Application.Services.LicenseService>();

            // Auto Backup Scheduler
            services.AddSingleton<IAutoBackupScheduler, AutoBackupScheduler>();

            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<SparePartListViewModel>();
            services.AddTransient<SparePartEditViewModel>();
            services.AddTransient<POSViewModel>();
            services.AddTransient<PurchaseInvoiceViewModel>();
            services.AddTransient<InvoiceListViewModel>();
            services.AddTransient<ReturnViewModel>();
            services.AddTransient<StockMovementViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<BackupViewModel>();
            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<LicenseActivationViewModel>();

            // Views - Windows
            services.AddTransient<LoginView>();
            services.AddTransient<MainWindow>();
            services.AddTransient<LicenseActivationView>();

            // Views - Pages (needed for navigation via DI)
            services.AddTransient<DashboardView>();
            services.AddTransient<SparePartListView>();
            services.AddTransient<SparePartEditView>();
            services.AddTransient<POSView>();
            services.AddTransient<Views.PurchaseInvoices.PurchaseInvoiceView>();
            services.AddTransient<InvoiceListView>();
            services.AddTransient<ReturnView>();
            services.AddTransient<StockMovementView>();
            services.AddTransient<ReportsView>();
            services.AddTransient<BackupView>();
            services.AddTransient<UserManagementView>();
            services.AddTransient<SettingsView>();
        });

        _host = builder.Build();
        Services = _host.Services;

        await _host.StartAsync();

        // ============================================
        // LICENSE CHECK - Must be before database init
        // ============================================
        var licenseService = Services.GetRequiredService<ILicenseService>();
        var licenseResult = await licenseService.ValidateLicenseAsync();

        if (!licenseResult.IsValid)
        {
            // Show specific message for clock tampering
            if (licenseResult.Status == LicenseStatus.ClockTampered)
            {
                System.Windows.MessageBox.Show(
                    "تم كشف تلاعب بتاريخ النظام!\n\nيرجى استعادة التاريخ الصحيح للجهاز لتتمكن من استخدام البرنامج.",
                    "تنبيه أمني",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            // Show license activation dialog
            var activationView = Services.GetRequiredService<LicenseActivationView>();
            var activationVm = Services.GetRequiredService<LicenseActivationViewModel>();

            activationView.DataContext = activationVm;
            activationView.SetCloseAction(() => { });

            // Keep showing activation dialog until license is valid or user closes
            bool? dialogResult;
            do
            {
                dialogResult = activationView.ShowDialog();

                // Re-check license after activation attempt
                licenseResult = await licenseService.ValidateLicenseAsync();

                if (licenseResult.IsValid)
                    break;

                // If user closed without activating, exit app
                if (dialogResult != true)
                {
                    Shutdown();
                    return;
                }

                // Create new view for retry
                activationView = Services.GetRequiredService<LicenseActivationView>();
                activationVm = Services.GetRequiredService<LicenseActivationViewModel>();
                activationView.DataContext = activationVm;
                activationView.SetCloseAction(() => { });

            } while (!licenseResult.IsValid);
        }
        else
        {
            // Periodic verification check
            await licenseService.CheckPeriodicVerificationAsync();

            // Show warning if license is about to expire
            if (licenseResult.License != null && licenseResult.License.DaysRemaining <= 7)
            {
                var daysLeft = licenseResult.License.DaysRemaining;
                var isTrial = licenseResult.License.IsTrial;
                var message = isTrial
                    ? $"متبقي {daysLeft} يوم على انتهاء فترة التجربة. يرجى تفعيل الترخيص للمتابعة."
                    : $"متبقي {daysLeft} يوم على انتهاء صلاحية الترخيص. يرجى تجديد الترخيص.";

                System.Windows.MessageBox.Show(message, "تنبيه الترخيص",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Initialize database and seed data
        try
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await DatabaseSeeder.SeedAsync(dbContext);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"حدث خطأ أثناء تهيئة قاعدة البيانات:\n{ex.Message}",
                "خطأ",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // Start auto backup scheduler if enabled
        try
        {
            var autoBackupScheduler = Services.GetRequiredService<IAutoBackupScheduler>();
            await autoBackupScheduler.StartAsync();
        }
        catch (Exception ex)
        {
            // Don't block app startup if auto backup fails to start
            System.Diagnostics.Debug.WriteLine($"Failed to start auto backup scheduler: {ex.Message}");
        }

        // Open LoginView after everything is initialized
        var loginView = Services.GetRequiredService<LoginView>();
        loginView.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        // Stop auto backup scheduler
        try
        {
            var autoBackupScheduler = Services.GetService<IAutoBackupScheduler>();
            if (autoBackupScheduler != null)
                await autoBackupScheduler.StopAsync();
        }
        catch { }

        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
