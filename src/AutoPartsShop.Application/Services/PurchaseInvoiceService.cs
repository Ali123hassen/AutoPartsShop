using AutoMapper;
using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.PurchaseInvoices;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Application.Specifications;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Enums;
using AutoPartsShop.Core.Exceptions;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Services;

public class PurchaseInvoiceService : IPurchaseInvoiceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public PurchaseInvoiceService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IAuditService auditService,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Returns the current user's id, falling back to any active admin user if no
    /// user context is set (e.g., for background jobs).
    /// </summary>
    private int GetCurrentUserId()
    {
        var id = _currentUserService.UserId;
        if (id.HasValue && id.Value > 0)
            return id.Value;

        var allUsers = _unitOfWork.Users.GetAllAsync().GetAwaiter().GetResult();
        var admin = allUsers.FirstOrDefault(u => u.IsActive);
        return admin?.Id ?? 1;
    }

    public async Task<PurchaseInvoiceDto> CreatePurchaseInvoiceAsync(CreatePurchaseInvoiceDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Generate invoice number
            // BUG FIX: include time-of-day (HHmmss) to avoid collisions between concurrent users.
            // Format: "PUR-yyyyMMdd-HHmmss" = 19 chars (fits nvarchar(20), safe even before
            // running Fix_InvoiceNumber_Length.sql).
            var invoiceCount = await _unitOfWork.PurchaseInvoices.CountAsync();
            var invoiceNumber = $"PUR-{DateTime.Now:yyyyMMdd-HHmmss}";

            var purchaseInvoice = new PurchaseInvoice
            {
                InvoiceNumber = invoiceNumber,
                InvoiceDate = DateTime.Now,
                UserId = GetCurrentUserId(),
                SupplierName = dto.SupplierName,
                SupplierPhone = dto.SupplierPhone,
                Notes = dto.Notes,
                Status = PurchaseInvoiceStatus.Completed
            };

            // Process each line item
            foreach (var itemDto in dto.Items)
            {
                var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(itemDto.SparePartId)
                    ?? throw new DomainException($"Spare part with ID {itemDto.SparePartId} not found.");

                var previousStock = sparePart.CurrentStock;

                // حفظ التكلفة والمخزون قبل التعديل (للاستخدام عند الإلغاء)
                var oldStock = sparePart.CurrentStock;
                var oldCost = sparePart.PurchasePrice;
                var newQty = itemDto.Quantity;
                var newCost = itemDto.CostPrice;

                // Weighted Average Cost = (oldStock × oldCost + newQty × newCost) / (oldStock + newQty)
                if (oldStock > 0 && oldCost > 0)
                {
                    var totalOldValue = oldStock * oldCost;
                    var totalNewValue = newQty * newCost;
                    sparePart.PurchasePrice = Math.Round((totalOldValue + totalNewValue) / (oldStock + newQty), 2);
                }
                else
                {
                    // First purchase or no existing stock — use the new cost directly
                    sparePart.PurchasePrice = newCost;
                }

                // Add stock
                sparePart.AddStock(itemDto.Quantity);

                // Update sale price if provided
                if (itemDto.SalePrice > 0)
                {
                    sparePart.SalePrice = itemDto.SalePrice;
                }

                // Update min sale price if provided
                if (itemDto.MinSalePrice.HasValue)
                {
                    sparePart.MinSalePrice = itemDto.MinSalePrice;
                }

                // Update last purchase date
                sparePart.LastPurchaseDate = DateTime.Now;

                // Update supplier info if provided
                if (!string.IsNullOrWhiteSpace(dto.SupplierName) && string.IsNullOrWhiteSpace(sparePart.SupplierName))
                {
                    sparePart.SupplierName = dto.SupplierName;
                }

                var newStock = sparePart.CurrentStock;
                await _unitOfWork.SpareParts.UpdateAsync(sparePart);

                // Create invoice item with previous cost snapshot
                var invoiceItem = new PurchaseInvoiceItem
                {
                    SparePartId = itemDto.SparePartId,
                    PartName = sparePart.Name,
                    Quantity = itemDto.Quantity,
                    CostPrice = itemDto.CostPrice,
                    SalePrice = itemDto.SalePrice,
                    MinSalePrice = itemDto.MinSalePrice,
                    LineTotal = itemDto.Quantity * itemDto.CostPrice,
                    PreviousStock = oldStock,          // حفظ المخزون قبل الشراء
                    PreviousCostPrice = oldCost         // حفظ التكلفة قبل الشراء
                };

                purchaseInvoice.Items.Add(invoiceItem);

                // Record stock movement
                var movement = new StockMovement
                {
                    SparePartId = sparePart.Id,
                    MovementType = MovementType.In,
                    Quantity = itemDto.Quantity,
                    PreviousStock = previousStock,
                    NewStock = newStock,
                    ReferenceType = "PurchaseInvoice",
                    Notes = $"Purchase - Invoice {invoiceNumber}",
                    UserId = purchaseInvoice.UserId
                };

                await _unitOfWork.StockMovements.AddAsync(movement);
            }

            // Calculate totals
            purchaseInvoice.CalculateTotals();

            // Save
            var addedInvoice = await _unitOfWork.PurchaseInvoices.AddAsync(purchaseInvoice);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();

            await _auditService.LogAsync("Create", "PurchaseInvoice", addedInvoice.Id,
                null, $"Created purchase invoice {invoiceNumber} with total {addedInvoice.TotalAmount:C}");

            return await MapToDtoAsync(addedInvoice.Id);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task CancelPurchaseInvoiceAsync(int purchaseInvoiceId)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var purchaseInvoice = await _unitOfWork.PurchaseInvoices.GetByIdAsync(purchaseInvoiceId)
                ?? throw new DomainException($"Purchase invoice with ID {purchaseInvoiceId} not found.");

            purchaseInvoice.Cancel();

            // Load items navigation property (GetByIdAsync doesn't include Items)
            var allItems = await _unitOfWork.PurchaseInvoiceItems.GetAllAsync();
            purchaseInvoice.Items = allItems.Where(i => i.PurchaseInvoiceId == purchaseInvoiceId).ToList();

            // Restore stock for each item (deduct what was added) AND reverse the
            // weighted-average cost back to the pre-purchase value.
            // نستخدم القيم المحفوظة في PurchaseInvoiceItem (PreviousStock و PreviousCostPrice)
            // بدلاً من الحساب العكسي الذي يفشل بعد عمليات شراء لاحقة.
            foreach (var item in purchaseInvoice.Items)
            {
                var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(item.SparePartId)
                    ?? throw new DomainException($"Spare part with ID {item.SparePartId} not found during cancellation.");

                var previousStock = sparePart.CurrentStock;

                if (sparePart.CurrentStock < item.Quantity)
                    throw new DomainException($"Cannot cancel: insufficient stock for '{sparePart.Name}'. Current: {sparePart.CurrentStock}, Need to reverse: {item.Quantity}.");

                // ===== استرجاع التكلفة =====
                // نحسب متوسط التكلفة مباشرة من فواتير الشراء النشطة المتبقية
                // بدلاً من الاعتماد على PreviousCostPrice الذي يصبح قديماً بعد إلغاء فواتير سابقة
                decimal restoredCost;

                var stockAfterDeduction = sparePart.CurrentStock - item.Quantity;

                if (stockAfterDeduction > 0)
                {
                    // نحسب التكلفة من فواتير الشراء المتبقية النشطة فقط
                    var allPurchaseItems = await _unitOfWork.PurchaseInvoiceItems.GetAllAsync();
                    var allPurchaseInvoices = await _unitOfWork.PurchaseInvoices.GetAllAsync();

                    // الحصول على معرفات فواتير الشراء النشطة (غير الملغاة)
                    var activeInvoiceIds = allPurchaseInvoices
                        .Where(pi => pi.Status == PurchaseInvoiceStatus.Completed)
                        .Select(pi => pi.Id)
                        .ToHashSet();

                    // استبعاد الفاتورة الحالية (قيد الإلغاء) وجمع عناصر نفس القطعة من الفواتير النشطة الأخرى
                    var remainingItems = allPurchaseItems
                        .Where(pi => pi.SparePartId == item.SparePartId
                                  && pi.PurchaseInvoiceId != purchaseInvoiceId
                                  && activeInvoiceIds.Contains(pi.PurchaseInvoiceId))
                        .ToList();

                    if (remainingItems.Count > 0)
                    {
                        // حساب متوسط التكلفة المرجح من الفواتير المتبقية
                        var totalRemainingQty = remainingItems.Sum(ri => ri.Quantity);
                        var totalRemainingValue = remainingItems.Sum(ri => ri.Quantity * ri.CostPrice);

                        if (totalRemainingQty > 0)
                        {
                            restoredCost = Math.Round(totalRemainingValue / totalRemainingQty, 2);
                        }
                        else
                        {
                            // لا توجد كميات متبقية ← نستخدم آخر تكلفة شراء
                            restoredCost = remainingItems.Max(ri => ri.CostPrice);
                        }
                    }
                    else
                    {
                        // لا توجد فواتير شراء أخرى ← نحتفظ بالتكلفة الحالية
                        restoredCost = sparePart.PurchasePrice;
                    }
                }
                else
                {
                    // لا يوجد مخزون متبقي ← نحتفظ بالتكلفة الحالية
                    restoredCost = sparePart.PurchasePrice;
                }

                sparePart.DeductStock(item.Quantity);
                sparePart.PurchasePrice = restoredCost;
                sparePart.UpdatedAt = DateTime.UtcNow;
                var newStock = sparePart.CurrentStock;

                await _unitOfWork.SpareParts.UpdateAsync(sparePart);

                // Record stock movement for the cancellation
                var movement = new StockMovement
                {
                    SparePartId = sparePart.Id,
                    MovementType = MovementType.Out,
                    Quantity = item.Quantity,
                    PreviousStock = previousStock,
                    NewStock = newStock,
                    ReferenceType = "PurchaseInvoiceCancellation",
                    ReferenceId = purchaseInvoiceId,
                    Notes = $"Stock reversed - Cancelled Purchase Invoice {purchaseInvoice.InvoiceNumber}. Cost reverted to {restoredCost}.",
                    UserId = purchaseInvoice.UserId
                };

                await _unitOfWork.StockMovements.AddAsync(movement);
            }

            await _unitOfWork.PurchaseInvoices.UpdateAsync(purchaseInvoice);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();

            await _auditService.LogAsync("Cancel", "PurchaseInvoice", purchaseInvoiceId,
                $"Status: Completed", $"Status: Cancelled - Stock reversed");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<PurchaseInvoiceDto?> GetByIdAsync(int id)
    {
        var invoice = await _unitOfWork.PurchaseInvoices.GetByIdAsync(id);
        if (invoice is null) return null;

        // Load navigation properties
        var allParts = await _unitOfWork.SpareParts.GetAllAsync();
        var allUsers = await _unitOfWork.Users.GetAllAsync();
        var allItems = await _unitOfWork.PurchaseInvoiceItems.GetAllAsync();

        var items = allItems.Where(i => i.PurchaseInvoiceId == id).ToList();
        invoice.Items = items;

        var user = allUsers.FirstOrDefault(u => u.Id == invoice.UserId);
        var dto = _mapper.Map<PurchaseInvoiceDto>(invoice);

        if (user != null)
            dto.UserName = user.Username;

        // Map items with part names
        dto.Items = items.Select(item =>
        {
            var itemDto = _mapper.Map<PurchaseInvoiceItemDto>(item);
            var part = allParts.FirstOrDefault(p => p.Id == item.SparePartId);
            if (part != null && string.IsNullOrEmpty(itemDto.PartName))
                itemDto.PartName = part.Name;
            return itemDto;
        }).ToList();

        return dto;
    }

    public async Task<PaginatedResult<PurchaseInvoiceDto>> GetPagedAsync(
        int pageNumber, int pageSize, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var allInvoices = await _unitOfWork.PurchaseInvoices.GetAllAsync();
        var allItems = await _unitOfWork.PurchaseInvoiceItems.GetAllAsync();
        var allUsers = await _unitOfWork.Users.GetAllAsync();

        // Apply date filters
        var filtered = allInvoices.AsEnumerable();
        if (fromDate.HasValue)
            filtered = filtered.Where(i => i.InvoiceDate >= fromDate.Value.Date);
        if (toDate.HasValue)
            filtered = filtered.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var totalCount = filtered.Count();
        var paged = filtered
            .OrderByDescending(i => i.InvoiceDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = paged.Select(invoice =>
        {
            var user = allUsers.FirstOrDefault(u => u.Id == invoice.UserId);
            var items = allItems.Where(i => i.PurchaseInvoiceId == invoice.Id).ToList();
            invoice.Items = items;

            var dto = _mapper.Map<PurchaseInvoiceDto>(invoice);
            if (user != null)
                dto.UserName = user.Username;

            dto.Items = items.Select(item => _mapper.Map<PurchaseInvoiceItemDto>(item)).ToList();
            return dto;
        }).ToList();

        return new PaginatedResult<PurchaseInvoiceDto>(dtos, totalCount, pageNumber, pageSize);
    }

    private async Task<PurchaseInvoiceDto> MapToDtoAsync(int invoiceId)
    {
        var dto = await GetByIdAsync(invoiceId);
        return dto ?? throw new DomainException($"Purchase invoice with ID {invoiceId} not found after creation.");
    }
}
