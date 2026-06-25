using AutoPartsShop.Application.Common;

namespace AutoPartsShop.Application.Interfaces;

/// <summary>
/// Provides functionality to reset the database by clearing transactional data
/// while preserving configuration data (users, roles, settings, etc.).
/// </summary>
public interface IDatabaseResetService
{
    /// <summary>
    /// Resets the database by deleting all transactional data while preserving:
    /// - Users, Roles, RolePermissions (system security)
    /// - Settings (app configuration)
    /// - Categories (catalog structure)
    /// - BackupHistory (audit trail of backups)
    /// - AuditLog (audit trail of operations)
    ///
    /// Tables that get cleared:
    /// - Invoices, InvoiceItems
    /// - PurchaseInvoices, PurchaseInvoiceItems
    /// - Returns
    /// - StockMovements
    /// - SpareParts
    /// </summary>
    /// <param name="createBackupFirst">If true, creates a backup file before resetting.</param>
    /// <param name="backupDirectory">Directory for the pre-reset backup (required if createBackupFirst is true).</param>
    /// <returns>A summary of what was reset.</returns>
    Task<DatabaseResetResult> ResetDatabaseAsync(bool createBackupFirst, string? backupDirectory);

    /// <summary>
    /// Returns a preview of what will be deleted and what will be kept.
    /// Use this to show the user a confirmation dialog before actually resetting.
    /// </summary>
    Task<DatabaseResetPreview> GetResetPreviewAsync();
}

/// <summary>
/// Preview of what will be deleted/kept during a reset.
/// </summary>
public class DatabaseResetPreview
{
    /// <summary>Tables that will be cleared, with their current row counts.</summary>
    public List<TablePreview> TablesToClear { get; set; } = new();

    /// <summary>Tables that will be preserved, with their current row counts.</summary>
    public List<TablePreview> TablesToKeep { get; set; } = new();

    /// <summary>Total rows that will be deleted.</summary>
    public int TotalRowsToBeDeleted => TablesToClear.Sum(t => t.RowCount);
}

public class TablePreview
{
    public string TableName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int RowCount { get; set; }
}

/// <summary>
/// Result of a database reset operation.
/// </summary>
public class DatabaseResetResult
{
    public bool IsSuccess { get; set; }
    public string? BackupPath { get; set; }
    public int TotalRowsDeleted { get; set; }
    public List<string> ClearedTables { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
}
