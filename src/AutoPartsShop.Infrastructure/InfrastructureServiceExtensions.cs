using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Core.Interfaces;
using AutoPartsShop.Infrastructure.Persistence;
using AutoPartsShop.Infrastructure.Repositories;
using AutoPartsShop.Infrastructure.Security;
using AutoPartsShop.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutoPartsShop.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString), ServiceLifetime.Transient);

        services.AddTransient(typeof(IRepository<>), typeof(Repository<>));
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        services.AddTransient<IPasswordHasher, PasswordHasher>();
        services.AddTransient<IBarcodeService, BarcodeGenerator>();
        services.AddTransient<IThermalPrinterService, ThermalPrinterService>();
        services.AddTransient<IDatabaseBackupExecutor, SqlServerBackupExecutor>();
        services.AddTransient<IDatabaseResetService, DatabaseResetService>();

        return services;
    }
}
