using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Application.Mappings;
using AutoPartsShop.Application.Services;
using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AutoPartsShop.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(MappingProfile));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationServiceExtensions).Assembly));
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceExtensions).Assembly);

        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<IUserService, UserService>();
        services.AddTransient<ISparePartService, SparePartService>();
        services.AddTransient<IInvoiceService, InvoiceService>();
        services.AddTransient<IPurchaseInvoiceService, PurchaseInvoiceService>();
        services.AddTransient<IReturnService, ReturnService>();
        services.AddTransient<IStockService, StockService>();
        services.AddTransient<IReportService, ReportService>();
        services.AddTransient<IBackupService, BackupService>();
        services.AddTransient<IAuditService, AuditService>();
        services.AddTransient<ISettingService, SettingService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<ICurrentUserService, CurrentUserService>();

        // License service is registered in App.xaml.cs (UI layer) because
        // IHardwareFingerprintService implementation is Windows-specific

        return services;
    }
}
