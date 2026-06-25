using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Interfaces;
using AutoPartsShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace AutoPartsShop.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    private IRepository<User>? _users;
    private IRepository<Role>? _roles;
    private IRepository<RolePermission>? _rolePermissions;
    private IRepository<Category>? _categories;
    private IRepository<SparePart>? _spareParts;
    private IRepository<Invoice>? _invoices;
    private IRepository<InvoiceItem>? _invoiceItems;
    private IRepository<Return>? _returns;
    private IRepository<StockMovement>? _stockMovements;
    private IRepository<AuditLog>? _auditLogs;
    private IRepository<Setting>? _settings;
    private IRepository<BackupHistory>? _backupHistories;
    private IRepository<PurchaseInvoice>? _purchaseInvoices;
    private IRepository<PurchaseInvoiceItem>? _purchaseInvoiceItems;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IRepository<User> Users => _users ??= new Repository<User>(_context);
    public IRepository<Role> Roles => _roles ??= new Repository<Role>(_context);
    public IRepository<RolePermission> RolePermissions => _rolePermissions ??= new Repository<RolePermission>(_context);
    public IRepository<Category> Categories => _categories ??= new Repository<Category>(_context);
    public IRepository<SparePart> SpareParts => _spareParts ??= new Repository<SparePart>(_context);
    public IRepository<Invoice> Invoices => _invoices ??= new Repository<Invoice>(_context);
    public IRepository<InvoiceItem> InvoiceItems => _invoiceItems ??= new Repository<InvoiceItem>(_context);
    public IRepository<Return> Returns => _returns ??= new Repository<Return>(_context);
    public IRepository<StockMovement> StockMovements => _stockMovements ??= new Repository<StockMovement>(_context);
    public IRepository<AuditLog> AuditLogs => _auditLogs ??= new Repository<AuditLog>(_context);
    public IRepository<Setting> Settings => _settings ??= new Repository<Setting>(_context);
    public IRepository<BackupHistory> BackupHistories => _backupHistories ??= new Repository<BackupHistory>(_context);
    public IRepository<PurchaseInvoice> PurchaseInvoices => _purchaseInvoices ??= new Repository<PurchaseInvoice>(_context);
    public IRepository<PurchaseInvoiceItem> PurchaseInvoiceItems => _purchaseInvoiceItems ??= new Repository<PurchaseInvoiceItem>(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
