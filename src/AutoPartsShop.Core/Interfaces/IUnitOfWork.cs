namespace AutoPartsShop.Core.Interfaces;

/// <summary>
/// Unit of Work pattern implementation that coordinates the work of multiple repositories
/// under a single database transaction.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>Repository for <see cref="Entities.User"/> entities.</summary>
    IRepository<Entities.User> Users { get; }

    /// <summary>Repository for <see cref="Entities.Role"/> entities.</summary>
    IRepository<Entities.Role> Roles { get; }

    /// <summary>Repository for <see cref="Entities.RolePermission"/> entities.</summary>
    IRepository<Entities.RolePermission> RolePermissions { get; }

    /// <summary>Repository for <see cref="Entities.Category"/> entities.</summary>
    IRepository<Entities.Category> Categories { get; }

    /// <summary>Repository for <see cref="Entities.SparePart"/> entities.</summary>
    IRepository<Entities.SparePart> SpareParts { get; }

    /// <summary>Repository for <see cref="Entities.Invoice"/> entities.</summary>
    IRepository<Entities.Invoice> Invoices { get; }

    /// <summary>Repository for <see cref="Entities.InvoiceItem"/> entities.</summary>
    IRepository<Entities.InvoiceItem> InvoiceItems { get; }

    /// <summary>Repository for <see cref="Entities.Return"/> entities.</summary>
    IRepository<Entities.Return> Returns { get; }

    /// <summary>Repository for <see cref="Entities.StockMovement"/> entities.</summary>
    IRepository<Entities.StockMovement> StockMovements { get; }

    /// <summary>Repository for <see cref="Entities.AuditLog"/> entities.</summary>
    IRepository<Entities.AuditLog> AuditLogs { get; }

    /// <summary>Repository for <see cref="Entities.Setting"/> entities.</summary>
    IRepository<Entities.Setting> Settings { get; }

    /// <summary>Repository for <see cref="Entities.BackupHistory"/> entities.</summary>
    IRepository<Entities.BackupHistory> BackupHistories { get; }

    /// <summary>Repository for <see cref="Entities.PurchaseInvoice"/> entities.</summary>
    IRepository<Entities.PurchaseInvoice> PurchaseInvoices { get; }

    /// <summary>Repository for <see cref="Entities.PurchaseInvoiceItem"/> entities.</summary>
    IRepository<Entities.PurchaseInvoiceItem> PurchaseInvoiceItems { get; }

    /// <summary>
    /// Saves all pending changes to the database.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync();

    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    Task BeginTransactionAsync();

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitTransactionAsync();

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackTransactionAsync();
}
