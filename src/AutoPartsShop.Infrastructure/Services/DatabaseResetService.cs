using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutoPartsShop.Infrastructure.Services;

/// <summary>
/// Implementation of IDatabaseResetService that clears transactional data
/// while preserving system configuration (users, roles, settings, etc.).
///
/// The reset uses DELETE (not TRUNCATE) so it works even when foreign-key
/// constraints would block TRUNCATE, and so we can run it inside a transaction
/// that we roll back on any error.
/// </summary>
public class DatabaseResetService : IDatabaseResetService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseResetService> _logger;
    private readonly IAuditService _auditService;

    // Tables to clear, in dependency order (children first, parents last)
    // so foreign-key constraints don't block the deletes.
    private static readonly string[] TablesToClear = new[]
    {
        "PurchaseInvoiceItems",
        "InvoiceItems",
        "StockMovements",
        "Returns",
        "Invoices",
        "PurchaseInvoices",
        "SpareParts"
        // NOTE: Categories, Settings, Users, Roles, RolePermissions, AuditLogs,
        // BackupHistories are PRESERVED.
    };

    // Arabic display names for the preview UI
    private static readonly Dictionary<string, string> TableDisplayNames = new()
    {
        { "Invoices", "فواتير المبيعات" },
        { "InvoiceItems", "أصناف فواتير المبيعات" },
        { "PurchaseInvoices", "فواتير المشتريات" },
        { "PurchaseInvoiceItems", "أصناف فواتير المشتريات" },
        { "Returns", "المرتجعات" },
        { "StockMovements", "حركات المخزون" },
        { "SpareParts", "قطع الغيار" },
        { "Categories", "الفئات" },
        { "Users", "المستخدمون" },
        { "Roles", "الأدوار" },
        { "RolePermissions", "صلاحيات الأدوار" },
        { "Settings", "الإعدادات" },
        { "AuditLogs", "سجل التدقيق" },
        { "BackupHistories", "سجل النسخ الاحتياطية" }
    };

    public DatabaseResetService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseResetService> logger,
        IAuditService auditService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _auditService = auditService;
    }

    public async Task<DatabaseResetPreview> GetResetPreviewAsync()
    {
        var preview = new DatabaseResetPreview();

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            // Build the list of tables to clear
            foreach (var table in TablesToClear)
            {
                var count = await GetRowCountAsync(connection, table);
                preview.TablesToClear.Add(new TablePreview
                {
                    TableName = table,
                    DisplayName = TableDisplayNames.TryGetValue(table, out var dn) ? dn : table,
                    RowCount = count
                });
            }

            // Build the list of tables to keep
            var tablesToKeep = new[] { "Users", "Roles", "RolePermissions", "Settings", "Categories", "AuditLogs", "BackupHistories" };
            foreach (var table in tablesToKeep)
            {
                var count = await GetRowCountAsync(connection, table);
                preview.TablesToKeep.Add(new TablePreview
                {
                    TableName = table,
                    DisplayName = TableDisplayNames.TryGetValue(table, out var dn) ? dn : table,
                    RowCount = count
                });
            }
        }
        finally
        {
            await connection.CloseAsync();
        }

        return preview;
    }

    public async Task<DatabaseResetResult> ResetDatabaseAsync(bool createBackupFirst, string? backupDirectory)
    {
        var startTime = DateTime.UtcNow;
        var result = new DatabaseResetResult();

        try
        {
            _logger.LogWarning("Database reset initiated by user. Backup first: {BackupFirst}", createBackupFirst);

            // ===== Step 1: Create a backup first if requested =====
            if (createBackupFirst)
            {
                if (string.IsNullOrWhiteSpace(backupDirectory))
                {
                    throw new InvalidOperationException("مسار النسخة الاحتياطية مطلوب عند تفعيل خيار النسخ قبل المسح");
                }

                using var backupScope = _serviceProvider.CreateScope();
                var backupService = backupScope.ServiceProvider.GetRequiredService<IBackupService>();
                _logger.LogInformation("Creating pre-reset backup in: {Dir}", backupDirectory);
                result.BackupPath = await backupService.CreateBackupAsync(backupDirectory);
                _logger.LogInformation("Pre-reset backup created: {Path}", result.BackupPath);
            }

            // ===== Step 2: Execute the reset inside a transaction =====
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                // Begin a transaction so we can roll back on any error
                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Disable foreign-key constraint checking during the bulk delete
                    // (we'll re-enable after). This avoids ordering issues and is safe
                    // because we're deleting ALL rows from these tables.
                    using (var disableCmd = connection.CreateCommand())
                    {
                        disableCmd.Transaction = transaction;
                        disableCmd.CommandText = "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'";
                        await disableCmd.ExecuteNonQueryAsync();
                    }

                    // Delete each table in order
                    foreach (var table in TablesToClear)
                    {
                        var rowCountBefore = await GetRowCountAsync(connection, table, transaction);

                        using var deleteCmd = connection.CreateCommand();
                        deleteCmd.Transaction = transaction;
                        deleteCmd.CommandText = $"DELETE FROM [{table}]";
                        deleteCmd.CommandTimeout = 300;  // 5 minutes per table
                        var deleted = await deleteCmd.ExecuteNonQueryAsync();

                        // DELETE returns rows affected; if it's -1 we use the count we measured before
                        if (deleted == -1) deleted = rowCountBefore;
                        result.TotalRowsDeleted += deleted;
                        result.ClearedTables.Add(table);

                        _logger.LogInformation("Cleared table {Table}: {Count} rows deleted", table, deleted);
                    }

                    // Re-enable foreign-key constraints
                    using (var enableCmd = connection.CreateCommand())
                    {
                        enableCmd.Transaction = transaction;
                        enableCmd.CommandText = "EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'";
                        await enableCmd.ExecuteNonQueryAsync();
                    }

                    // Commit the transaction
                    await transaction.CommitAsync();
                }
                catch
                {
                    // Roll back on any error
                    try { await transaction.RollbackAsync(); }
                    catch { /* ignore rollback errors */ }
                    throw;
                }
            }
            finally
            {
                await connection.CloseAsync();
            }

            // ===== Step 3: Log the operation to the audit log =====
            try
            {
                await _auditService.LogAsync("ResetDatabase", "Database", null,
                    null,
                    $"Database reset completed. {result.TotalRowsDeleted} rows deleted from {result.ClearedTables.Count} tables. " +
                    $"Backup: {result.BackupPath ?? "(none)"}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write audit log for database reset (reset itself succeeded)");
            }

            result.IsSuccess = true;
            result.Duration = DateTime.UtcNow - startTime;
            _logger.LogWarning("Database reset completed in {Duration}. Total rows deleted: {Count}",
                result.Duration, result.TotalRowsDeleted);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database reset failed");
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }
    }

    /// <summary>
    /// Gets the row count of a table. Uses an open connection with an optional transaction.
    /// </summary>
    private async Task<int> GetRowCountAsync(System.Data.Common.DbConnection connection, string tableName, System.Data.Common.DbTransaction? transaction = null)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            if (transaction != null) cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT COUNT(*) FROM [{tableName}]";
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }
        catch
        {
            // Table might not exist or other issue — return 0
            return 0;
        }
    }
}
