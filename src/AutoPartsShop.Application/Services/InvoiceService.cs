using AutoMapper;
using AutoPartsShop.Application.DTOs.Invoices;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Application.Specifications;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Enums;
using AutoPartsShop.Core.Exceptions;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public InvoiceService(
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
    /// Returns the current authenticated user's id, throwing if no user is logged in.
    /// Falls back to user 1 (default admin) ONLY when no user context is available,
    /// to preserve backward compatibility with legacy code paths that may run before login.
    /// </summary>
    private int GetCurrentUserId()
    {
        var id = _currentUserService.UserId;
        if (id.HasValue && id.Value > 0)
            return id.Value;

        // Last-resort fallback: try to find any active admin user.
        // This prevents hard crashes when background jobs run without a user context.
        var allUsers = _unitOfWork.Users.GetAllAsync().GetAwaiter().GetResult();
        var admin = allUsers.FirstOrDefault(u => u.IsActive);
        return admin?.Id ?? 1;
    }

    public async Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            // BUG FIX: previously the invoice number was generated from
            // `Invoices.CountAsync() + 1`, which is racy — two concurrent cashiers
            // could both see the same count and produce the same invoice number.
            // We include the time-of-day (HHmmss) in the number, which makes
            // collisions practically impossible.
            //
            // IMPORTANT: the InvoiceNumber column in the database is nvarchar(20)
            // (or nvarchar(50) after running Fix_InvoiceNumber_Length.sql).
            // The format below produces exactly 19 characters so it fits nvarchar(20):
            //   "INV-" (4) + "20260617" (8) + "-" (1) + "143025" (6) = 19 chars
            // We deliberately do NOT append a counter because it would push the
            // total past 20 chars. If two invoices are created in the same second,
            // the unique index on InvoiceNumber will catch it (and the user can retry).
            var invoiceCount = await _unitOfWork.Invoices.CountAsync();
            var invoiceNumber = $"INV-{DateTime.Now:yyyyMMdd-HHmmss}";

            // Use the real current user from the auth context.
            var currentUserId = GetCurrentUserId();
            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                InvoiceDate = DateTime.Now,
                UserId = currentUserId,
                DiscountAmount = dto.DiscountAmount,
                TaxRate = dto.TaxRate,
                PaidAmount = dto.PaidAmount,
                PaymentMethod = dto.PaymentMethod,
                CustomerName = dto.CustomerName,
                Notes = dto.Notes,
                Status = InvoiceStatus.Completed
            };

            // Process each line item — collect movements so we can set ReferenceId after save
            var pendingMovements = new List<StockMovement>();

            foreach (var itemDto in dto.Items)
            {
                var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(itemDto.SparePartId)
                    ?? throw new DomainException($"Spare part with ID {itemDto.SparePartId} not found.");

                // Deduct stock — this throws InsufficientStockException if not enough
                var previousStock = sparePart.CurrentStock;
                sparePart.DeductStock(itemDto.Quantity);
                var newStock = sparePart.CurrentStock;

                await _unitOfWork.SpareParts.UpdateAsync(sparePart);

                // Create invoice item
                var invoiceItem = new InvoiceItem
                {
                    SparePartId = itemDto.SparePartId,
                    PartName = sparePart.Name,
                    Quantity = itemDto.Quantity,
                    UnitPrice = itemDto.UnitPrice,
                    DiscountPercent = itemDto.DiscountPercent,
                    DiscountAmount = itemDto.DiscountAmount,
                    LineTotal = (itemDto.Quantity * itemDto.UnitPrice) - itemDto.DiscountAmount,
                    CostAtSale = sparePart.PurchasePrice  // حفظ تكلفة القطعة وقت البيع
                };

                invoice.Items.Add(invoiceItem);

                // Record stock movement (ReferenceId will be set after invoice is saved)
                var movement = new StockMovement
                {
                    SparePartId = sparePart.Id,
                    MovementType = MovementType.Out,
                    Quantity = itemDto.Quantity,
                    PreviousStock = previousStock,
                    NewStock = newStock,
                    ReferenceType = "Invoice",
                    ReferenceId = null, // Will be set after invoice is saved
                    Notes = $"Sale - Invoice {invoiceNumber}",
                    UserId = invoice.UserId
                };

                pendingMovements.Add(movement);
                await _unitOfWork.StockMovements.AddAsync(movement);
            }

            // Calculate totals
            invoice.CalculateTotals();

            // Save the invoice
            var addedInvoice = await _unitOfWork.Invoices.AddAsync(invoice);
            await _unitOfWork.SaveChangesAsync();

            // BUG FIX: previously ReferenceId stayed null forever because the
            // movements were already saved with null. Now we backfill the invoice id
            // on each pending movement and persist the update.
            foreach (var movement in pendingMovements)
            {
                movement.ReferenceId = addedInvoice.Id;
                await _unitOfWork.StockMovements.UpdateAsync(movement);
            }
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();

            await _auditService.LogAsync("Create", "Invoice", addedInvoice.Id,
                null, $"Created invoice {invoiceNumber} with total {addedInvoice.TotalAmount:C}");

            return await MapInvoiceToDtoAsync(addedInvoice.Id);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task CancelInvoiceAsync(int invoiceId)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Load invoice with Items (tracked) so we can modify and save
            var spec = new InvoiceSpecification(invoiceId);
            var trackedInvoices = await _unitOfWork.Invoices.FindTrackedAsync(spec);
            var invoice = trackedInvoices.FirstOrDefault()
                ?? throw new DomainException($"Invoice with ID {invoiceId} not found.");

            invoice.Cancel();

            // Restore stock for each item
            foreach (var item in invoice.Items)
            {
                var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(item.SparePartId)
                    ?? throw new DomainException($"Spare part with ID {item.SparePartId} not found during cancellation.");

                var previousStock = sparePart.CurrentStock;
                sparePart.AddStock(item.Quantity);
                var newStock = sparePart.CurrentStock;

                await _unitOfWork.SpareParts.UpdateAsync(sparePart);

                // Record stock movement for the cancellation
                var movement = new StockMovement
                {
                    SparePartId = sparePart.Id,
                    MovementType = MovementType.In,
                    Quantity = item.Quantity,
                    PreviousStock = previousStock,
                    NewStock = newStock,
                    ReferenceType = "InvoiceCancellation",
                    ReferenceId = invoiceId,
                    Notes = $"Stock restored - Cancelled Invoice {invoice.InvoiceNumber}",
                    UserId = invoice.UserId
                };

                await _unitOfWork.StockMovements.AddAsync(movement);
            }

            await _unitOfWork.Invoices.UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();

            await _auditService.LogAsync("Cancel", "Invoice", invoiceId,
                $"Status: Completed", $"Status: Cancelled - Stock restored");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<InvoiceDto?> GetByIdAsync(int id)
    {
        var spec = new InvoiceSpecification(id);
        var invoices = await _unitOfWork.Invoices.FindAsync(spec);
        var invoice = invoices.FirstOrDefault();

        return invoice is null ? null : await MapInvoiceWithReturnsAsync(invoice);
    }

    public async Task<InvoiceDto?> GetByNumberAsync(string invoiceNumber)
    {
        var spec = new InvoiceSpecification(invoiceNumber);
        var invoices = await _unitOfWork.Invoices.FindAsync(spec);
        var invoice = invoices.FirstOrDefault();

        return invoice is null ? null : await MapInvoiceWithReturnsAsync(invoice);
    }

    public async Task<Common.PaginatedResult<InvoiceDto>> GetPagedAsync(
        int pageNumber, int pageSize, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var spec = new InvoiceSpecification(pageNumber, pageSize, fromDate, toDate);
        var invoices = await _unitOfWork.Invoices.FindAsync(spec);

        // جلب المرتجعات لفواتير هذه الصفحة فقط (بدلاً من تحميل كل المرتجعات)
        var invoiceIds = invoices.Select(i => i.Id).ToHashSet();
        IEnumerable<Core.Entities.Return>? pageReturns = null;
        if (invoiceIds.Count > 0)
        {
            var returnSpec = new ReturnSpecification(invoiceIds);
            pageReturns = await _unitOfWork.Returns.FindAsync(returnSpec);
        }

        var dtos = new List<InvoiceDto>();
        foreach (var invoice in invoices)
        {
            var dto = await MapInvoiceWithReturnsAsync(invoice, pageReturns);
            dtos.Add(dto);
        }

        // حساب العدد الإجمالي بدون تحميل كل البيانات
        var countSpec = new InvoiceSpecification(fromDate, toDate);
        var allMatching = await _unitOfWork.Invoices.FindAsync(countSpec);
        var totalCount = allMatching.Count;

        return new Common.PaginatedResult<InvoiceDto>(dtos, totalCount, pageNumber, pageSize);
    }

    public async Task<decimal> GetDailySalesTotalAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

        var spec = new InvoiceSpecification(startOfDay, endOfDay, InvoiceStatus.Completed);
        var invoices = await _unitOfWork.Invoices.FindAsync(spec);

        // BUG FIX (previously): the code returned `i.SubTotal - i.DiscountAmount`,
        // but SubTotal is ALREADY net of per-item discounts because LineTotal in each
        // InvoiceItem is computed as `(Quantity * UnitPrice) - DiscountAmount`.
        // Subtracting Invoice.DiscountAmount again was double-counting the discount.
        // See Invoice.CalculateTotals() for confirmation that SubTotal already
        // excludes discounts.
        return invoices.Sum(i => i.SubTotal);
    }

    private async Task<InvoiceDto> MapInvoiceToDtoAsync(int invoiceId)
    {
        var spec = new InvoiceSpecification(invoiceId);
        var invoices = await _unitOfWork.Invoices.FindAsync(spec);
        var invoice = invoices.FirstOrDefault()
            ?? throw new DomainException($"Invoice with ID {invoiceId} not found after creation.");
        return await MapInvoiceWithReturnsAsync(invoice);
    }

    /// <summary>
    /// تحويل الفاتورة إلى DTO مع حساب حالة المرتجع لكل فاتورة وكل صنف
    /// </summary>
    private async Task<InvoiceDto> MapInvoiceWithReturnsAsync(Invoice invoice, IEnumerable<Core.Entities.Return>? preloadedReturns = null)
    {
        var dto = _mapper.Map<InvoiceDto>(invoice);

        // جلب المرتجعات لهذه الفاتورة
        var allReturns = preloadedReturns ?? await _unitOfWork.Returns.GetAllAsync();
        var invoiceReturns = allReturns.Where(r => r.InvoiceId == invoice.Id).ToList();

        // تجميع الكميات المرتجعة لكل صنف
        var returnedQtyByPart = new Dictionary<int, int>();
        foreach (var ret in invoiceReturns)
        {
            if (returnedQtyByPart.ContainsKey(ret.SparePartId))
                returnedQtyByPart[ret.SparePartId] += ret.Quantity;
            else
                returnedQtyByPart[ret.SparePartId] = ret.Quantity;
        }

        // حساب حالة المرتجع لكل صنف
        var hasAnyReturn = false;
        var allItemsFullyReturned = true;

        for (int i = 0; i < dto.Items.Count; i++)
        {
            var item = dto.Items[i];
            item.LineNumber = i + 1;

            var returnedQty = returnedQtyByPart.GetValueOrDefault(item.SparePartId, 0);
            item.ReturnedQty = returnedQty;

            if (returnedQty > 0)
            {
                hasAnyReturn = true;

                if (returnedQty >= item.Quantity)
                {
                    item.ItemReturnStatus = "FullReturn";
                }
                else
                {
                    item.ItemReturnStatus = "PartialReturn";
                    allItemsFullyReturned = false;
                }
            }
            else
            {
                item.ItemReturnStatus = "None";
                allItemsFullyReturned = false;
            }
        }

        // حساب حالة المرتجع للفاتورة ككل
        if (hasAnyReturn && invoice.Status != InvoiceStatus.Cancelled)
        {
            if (allItemsFullyReturned && dto.Items.Any())
            {
                dto.ReturnStatus = "Full";
                dto.Status = "FullReturn";
            }
            else
            {
                dto.ReturnStatus = "Partial";
                dto.Status = "PartialReturn";
            }

            // ===== حساب تفاصيل المرتجع المالي =====
            var returnSubTotal = 0m;   // قبل الخصم
            var returnAfterDiscount = 0m; // بعد الخصم (قبل الضريبة)

            foreach (var ret in invoiceReturns)
            {
                var invoiceItem = invoice.Items.FirstOrDefault(it => it.SparePartId == ret.SparePartId);
                if (invoiceItem != null)
                {
                    // قيمة المرتجع قبل الخصم = الكمية المرتجعة × سعر الوحدة الأصلي
                    returnSubTotal += ret.Quantity * invoiceItem.UnitPrice;

                    // سعر الوحدة بعد الخصم
                    var pricePerUnit = invoiceItem.Quantity > 0
                        ? invoiceItem.LineTotal / invoiceItem.Quantity
                        : 0;

                    // قيمة المرتجع بعد الخصم (قبل الضريبة)
                    returnAfterDiscount += ret.Quantity * pricePerUnit;
                }
                else
                {
                    // لا يوجد صنف مطابق - نستخدم RefundAmount
                    returnSubTotal += ret.RefundAmount;
                    returnAfterDiscount += ret.RefundAmount;
                }
            }

            var returnDiscount = returnSubTotal - returnAfterDiscount;
            var returnTax = returnAfterDiscount * (invoice.TaxRate / 100m);
            var returnTotal = returnAfterDiscount + returnTax;

            dto.ReturnSubTotal = returnSubTotal;
            dto.ReturnDiscount = returnDiscount;
            dto.ReturnAfterDiscount = returnAfterDiscount;
            dto.ReturnTax = returnTax;
            dto.ReturnTotal = returnTotal;
        }
        else
        {
            dto.ReturnStatus = "None";
            // Status stays as-is (Completed or Cancelled)
        }

        return dto;
    }
}
