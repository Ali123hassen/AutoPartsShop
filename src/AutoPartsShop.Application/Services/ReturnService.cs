using AutoMapper;
using AutoPartsShop.Application.DTOs.Returns;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Application.Specifications;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Enums;
using AutoPartsShop.Core.Exceptions;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Services;

public class ReturnService : IReturnService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public ReturnService(
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

    public async Task<ReturnDto> CreateReturnAsync(CreateReturnDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Validate spare part exists
            var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(dto.SparePartId)
                ?? throw new DomainException($"Spare part with ID {dto.SparePartId} not found.");

            // Validate invoice if provided
            if (dto.InvoiceId.HasValue)
            {
                var invoice = await _unitOfWork.Invoices.GetByIdAsync(dto.InvoiceId.Value)
                    ?? throw new DomainException($"Invoice with ID {dto.InvoiceId.Value} not found.");

                // منع المرتجع على فاتورة ملغاة
                if (invoice.Status == InvoiceStatus.Cancelled)
                    throw new DomainException("لا يمكن عمل مرتجع على فاتورة ملغاة. الفاتورة رقم " + invoice.InvoiceNumber + " تم إلغاؤها مسبقاً.");

                // BUG FIX (previously): the code only ran the availability check
                // when invoiceItem != null. If the part was NOT in the invoice,
                // validation was silently skipped, allowing customers to return
                // items that were never on the original invoice. Now we throw.
                var returnedQty = await GetReturnedQuantityAsync(dto.InvoiceId.Value, dto.SparePartId);
                var invoiceItems = await _unitOfWork.InvoiceItems.GetAllAsync();
                var invoiceItem = invoiceItems.FirstOrDefault(i => i.InvoiceId == dto.InvoiceId.Value && i.SparePartId == dto.SparePartId);
                if (invoiceItem == null)
                {
                    throw new DomainException(
                        $"Spare part with ID {dto.SparePartId} was not found on invoice {dto.InvoiceId.Value}. " +
                        "Cannot return an item that was not part of the original sale.");
                }

                var available = invoiceItem.Quantity - returnedQty;
                if (dto.Quantity > available)
                    throw new DomainException($"Cannot return {dto.Quantity} items. Only {available} available for return (Sold: {invoiceItem.Quantity}, Already returned: {returnedQty}).");
            }

            // Validate replacement part if exchange
            if (dto.ReturnType == ReturnType.Exchange && dto.ReplacementPartId.HasValue)
            {
                var replacementPart = await _unitOfWork.SpareParts.GetByIdAsync(dto.ReplacementPartId.Value)
                    ?? throw new DomainException($"Replacement spare part with ID {dto.ReplacementPartId.Value} not found.");

                // For exchange, deduct stock of the replacement part
                if (replacementPart.CurrentStock < dto.Quantity)
                    throw new InsufficientStockException(replacementPart.Name, replacementPart.CurrentStock, dto.Quantity);

                var previousStockReplacement = replacementPart.CurrentStock;
                replacementPart.DeductStock(dto.Quantity);
                var newStockReplacement = replacementPart.CurrentStock;

                await _unitOfWork.SpareParts.UpdateAsync(replacementPart);

                // Record stock movement for the replacement part going out
                var outMovement = new StockMovement
                {
                    SparePartId = replacementPart.Id,
                    MovementType = MovementType.Out,
                    Quantity = dto.Quantity,
                    PreviousStock = previousStockReplacement,
                    NewStock = newStockReplacement,
                    ReferenceType = "Return",
                    Notes = $"Exchange - Replacement part out",
                    UserId = GetCurrentUserId()
                };
                await _unitOfWork.StockMovements.AddAsync(outMovement);
            }

            // Return the original part to stock
            var previousStock = sparePart.CurrentStock;
            sparePart.AddStock(dto.Quantity);
            var newStock = sparePart.CurrentStock;

            await _unitOfWork.SpareParts.UpdateAsync(sparePart);

            // Record stock movement for the returned part coming in
            var inMovement = new StockMovement
            {
                SparePartId = sparePart.Id,
                MovementType = MovementType.Return,
                Quantity = dto.Quantity,
                PreviousStock = previousStock,
                NewStock = newStock,
                ReferenceType = "Return",
                Notes = dto.ReturnType == ReturnType.Refund
                    ? "Refund - Part returned to stock"
                    : "Exchange - Original part returned to stock",
                UserId = GetCurrentUserId()
            };
            await _unitOfWork.StockMovements.AddAsync(inMovement);

            // Generate return number
            // BUG FIX: include time-of-day (HHmmss) to avoid collisions when two users
            // create returns concurrently. Format: "RET-yyyyMMdd-HHmmss" = 19 chars
            // (fits nvarchar(20) without modification, or nvarchar(50) after running
            // Fix_InvoiceNumber_Length.sql for extra safety).
            var returnCount = await _unitOfWork.Returns.CountAsync();
            var returnNumber = $"RET-{DateTime.Now:yyyyMMdd-HHmmss}";

            // Create the return record
            var returnEntity = new Return
            {
                ReturnNumber = returnNumber,
                InvoiceId = dto.InvoiceId,
                ReturnDate = DateTime.Now,
                ReturnType = dto.ReturnType,
                SparePartId = dto.SparePartId,
                ReplacementPartId = dto.ReturnType == ReturnType.Exchange ? dto.ReplacementPartId : null,
                Quantity = dto.Quantity,
                RefundAmount = dto.RefundAmount,
                Reason = dto.Reason,
                UserId = GetCurrentUserId()
            };

            var addedReturn = await _unitOfWork.Returns.AddAsync(returnEntity);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();

            await _auditService.LogAsync("Create", "Return", addedReturn.Id,
                null, $"Created return {returnNumber}: {dto.ReturnType} - {sparePart.Name} x{dto.Quantity}");

            // Re-fetch with navigation properties
            var fetched = await _unitOfWork.Returns.GetByIdAsync(addedReturn.Id);
            return fetched is null ? _mapper.Map<ReturnDto>(addedReturn) : _mapper.Map<ReturnDto>(fetched);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<List<ReturnDto>> CreateBatchReturnAsync(CreateBatchReturnDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            throw new DomainException("No items selected for return.");

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Validate invoice exists
            var invoiceSpec = new InvoiceSpecification(dto.InvoiceId);
            var invoices = await _unitOfWork.Invoices.FindAsync(invoiceSpec);
            var invoice = invoices.FirstOrDefault()
                ?? throw new DomainException($"Invoice with ID {dto.InvoiceId} not found.");

            // منع المرتجع على فاتورة ملغاة
            if (invoice.Status == InvoiceStatus.Cancelled)
                throw new DomainException("لا يمكن عمل مرتجع على فاتورة ملغاة. الفاتورة رقم " + invoice.InvoiceNumber + " تم إلغاؤها مسبقاً.");

            var results = new List<ReturnDto>();
            var returnCount = await _unitOfWork.Returns.CountAsync();

            foreach (var item in dto.Items)
            {
                // Validate spare part exists
                var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(item.SparePartId)
                    ?? throw new DomainException($"Spare part with ID {item.SparePartId} not found.");

                // Get returned quantity for this item in this invoice
                var returnedQty = await GetReturnedQuantityAsync(dto.InvoiceId, item.SparePartId);

                // Find the invoice item
                var invoiceItem = invoice.Items.FirstOrDefault(i => i.SparePartId == item.SparePartId)
                    ?? throw new DomainException($"Item with SparePartId {item.SparePartId} not found in invoice {dto.InvoiceId}.");

                // Validate return quantity
                var available = invoiceItem.Quantity - returnedQty;
                if (item.Quantity > available)
                    throw new DomainException($"Cannot return {item.Quantity} of '{invoiceItem.PartName}'. Only {available} available for return (Sold: {invoiceItem.Quantity}, Already returned: {returnedQty}).");
                if (item.Quantity <= 0)
                    throw new DomainException($"Return quantity must be greater than 0 for '{invoiceItem.PartName}'.");

                // Handle exchange replacement part
                if (dto.ReturnType == ReturnType.Exchange && dto.ReplacementPartId.HasValue)
                {
                    var replacementPart = await _unitOfWork.SpareParts.GetByIdAsync(dto.ReplacementPartId.Value)
                        ?? throw new DomainException($"Replacement spare part with ID {dto.ReplacementPartId.Value} not found.");

                    if (replacementPart.CurrentStock < item.Quantity)
                        throw new InsufficientStockException(replacementPart.Name, replacementPart.CurrentStock, item.Quantity);

                    var prevRepStock = replacementPart.CurrentStock;
                    replacementPart.DeductStock(item.Quantity);
                    await _unitOfWork.SpareParts.UpdateAsync(replacementPart);

                    var outMovement = new StockMovement
                    {
                        SparePartId = replacementPart.Id,
                        MovementType = MovementType.Out,
                        Quantity = item.Quantity,
                        PreviousStock = prevRepStock,
                        NewStock = replacementPart.CurrentStock,
                        ReferenceType = "Return",
                        Notes = $"Exchange - Replacement part out",
                        UserId = GetCurrentUserId()
                    };
                    await _unitOfWork.StockMovements.AddAsync(outMovement);
                }

                // Return the original part to stock
                var previousStock = sparePart.CurrentStock;
                sparePart.AddStock(item.Quantity);
                await _unitOfWork.SpareParts.UpdateAsync(sparePart);

                var inMovement = new StockMovement
                {
                    SparePartId = sparePart.Id,
                    MovementType = MovementType.Return,
                    Quantity = item.Quantity,
                    PreviousStock = previousStock,
                    NewStock = sparePart.CurrentStock,
                    ReferenceType = "Return",
                    Notes = dto.ReturnType == ReturnType.Refund
                        ? "Refund - Part returned to stock"
                        : "Exchange - Original part returned to stock",
                    UserId = GetCurrentUserId()
                };
                await _unitOfWork.StockMovements.AddAsync(inMovement);

                // Generate return number
                returnCount++;
                var returnNumber = $"RET-{DateTime.Now:yyyyMMdd-HHmmss}-{returnCount:D2}";

                // Create the return record
                var returnEntity = new Return
                {
                    ReturnNumber = returnNumber,
                    InvoiceId = dto.InvoiceId,
                    ReturnDate = DateTime.Now,
                    ReturnType = dto.ReturnType,
                    SparePartId = item.SparePartId,
                    ReplacementPartId = dto.ReturnType == ReturnType.Exchange ? dto.ReplacementPartId : null,
                    Quantity = item.Quantity,
                    RefundAmount = item.RefundAmount,
                    Reason = dto.Reason,
                    UserId = GetCurrentUserId()
                };

                var addedReturn = await _unitOfWork.Returns.AddAsync(returnEntity);
                await _unitOfWork.SaveChangesAsync();

                results.Add(_mapper.Map<ReturnDto>(addedReturn));

                await _auditService.LogAsync("Create", "Return", addedReturn.Id,
                    null, $"Created return {returnNumber}: {dto.ReturnType} - {sparePart.Name} x{item.Quantity}");
            }

            await _unitOfWork.CommitTransactionAsync();
            return results;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<List<InvoiceReturnItemDto>> GetInvoiceReturnItemsAsync(int invoiceId)
    {
        // Get invoice with items
        var invoiceSpec = new InvoiceSpecification(invoiceId);
        var invoices = await _unitOfWork.Invoices.FindAsync(invoiceSpec);
        var invoice = invoices.FirstOrDefault()
            ?? throw new DomainException($"Invoice with ID {invoiceId} not found.");

        // Get all returns for this invoice
        var returnSpec = new ReturnSpecification(invoiceId);
        var returns = await _unitOfWork.Returns.FindAsync(returnSpec);

        // Calculate previously returned quantity per spare part
        var returnedQuantities = returns
            .GroupBy(r => r.SparePartId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

        // Get all spare parts to find part numbers
        var allParts = await _unitOfWork.SpareParts.GetAllAsync();
        var partDict = allParts.ToDictionary(p => p.Id);

        // Build the result
        var result = new List<InvoiceReturnItemDto>();
        foreach (var item in invoice.Items)
        {
            var previouslyReturned = returnedQuantities.GetValueOrDefault(item.SparePartId, 0);
            var partNumber = partDict.TryGetValue(item.SparePartId, out var part) ? part.PartNumber : "";

            result.Add(new InvoiceReturnItemDto
            {
                SparePartId = item.SparePartId,
                PartName = item.PartName,
                PartNumber = partNumber,
                SoldQuantity = item.Quantity,
                PreviouslyReturnedQuantity = previouslyReturned,
                UnitPrice = item.UnitPrice,
                DiscountPercent = item.DiscountPercent,
                DiscountAmount = item.DiscountAmount,
                LineTotal = item.LineTotal,
                TaxRate = invoice.TaxRate
            });
        }

        return result;
    }

    public async Task<ReturnDto?> GetByIdAsync(int id)
    {
        var returnEntity = await _unitOfWork.Returns.GetByIdAsync(id);
        return returnEntity is null ? null : _mapper.Map<ReturnDto>(returnEntity);
    }

    public async Task<ReturnDetailDto?> GetReturnDetailAsync(int id)
    {
        var returnEntity = await _unitOfWork.Returns.GetByIdAsync(id);
        if (returnEntity is null) return null;

        // Load navigation properties if not already loaded
        var allParts = await _unitOfWork.SpareParts.GetAllAsync();
        var allUsers = await _unitOfWork.Users.GetAllAsync();

        if (returnEntity.SparePart == null)
        {
            returnEntity.SparePart = allParts.FirstOrDefault(p => p.Id == returnEntity.SparePartId) ?? throw new DomainException($"Spare part with ID {returnEntity.SparePartId} not found.");
        }
        if (returnEntity.User == null)
        {
            returnEntity.User = allUsers.FirstOrDefault(u => u.Id == returnEntity.UserId) ?? throw new DomainException($"User with ID {returnEntity.UserId} not found.");
        }
        if (returnEntity.ReplacementPartId.HasValue && returnEntity.ReplacementPart == null)
        {
            returnEntity.ReplacementPart = allParts.FirstOrDefault(p => p.Id == returnEntity.ReplacementPartId.Value);
        }

        var detail = new ReturnDetailDto
        {
            Id = returnEntity.Id,
            ReturnNumber = returnEntity.ReturnNumber,
            ReturnDate = returnEntity.ReturnDate,
            ReturnType = returnEntity.ReturnType.ToString(),
            Quantity = returnEntity.Quantity,
            RefundAmount = returnEntity.RefundAmount,
            Reason = returnEntity.Reason,
            PartName = returnEntity.SparePart?.Name ?? string.Empty,
            PartNumber = returnEntity.SparePart?.PartNumber,
            ReplacementPartName = returnEntity.ReplacementPart?.Name,
            UserName = returnEntity.User?.Username ?? string.Empty
        };

        // Load invoice and item details for financial breakdown
        if (returnEntity.InvoiceId.HasValue)
        {
            var invoiceSpec = new InvoiceSpecification(returnEntity.InvoiceId.Value);
            var invoices = await _unitOfWork.Invoices.FindAsync(invoiceSpec);
            var invoice = invoices.FirstOrDefault();

            if (invoice != null)
            {
                detail.InvoiceNumber = invoice.InvoiceNumber;
                detail.InvoiceDate = invoice.InvoiceDate;
                detail.CustomerName = invoice.CustomerName;
                detail.InvoiceUserName = invoice.User?.Username;

                // Find the matching invoice item
                var invoiceItem = invoice.Items.FirstOrDefault(i => i.SparePartId == returnEntity.SparePartId);
                if (invoiceItem != null)
                {
                    detail.UnitPrice = invoiceItem.UnitPrice;
                    detail.DiscountPercent = invoiceItem.DiscountPercent;

                    // Calculate proportional discount for the returned quantity
                    // DiscountAmount is the total discount for the entire line (all SoldQuantity)
                    // So per-unit discount = DiscountAmount / SoldQuantity
                    var discountPerUnit = invoiceItem.Quantity > 0
                        ? invoiceItem.DiscountAmount / invoiceItem.Quantity
                        : 0;
                    detail.DiscountAmount = discountPerUnit * returnEntity.Quantity;

                    detail.PriceAfterDiscount = invoiceItem.LineTotal / invoiceItem.Quantity;
                    detail.TaxRate = invoice.TaxRate;
                    detail.SubtotalBeforeTax = detail.PriceAfterDiscount * returnEntity.Quantity;
                    detail.TaxAmount = detail.SubtotalBeforeTax * (invoice.TaxRate / 100m);
                    detail.TotalRefundAmount = detail.SubtotalBeforeTax + detail.TaxAmount;
                }
            }
        }
        else
        {
            // No invoice linked  use RefundAmount as the total
            detail.TotalRefundAmount = returnEntity.RefundAmount;
            detail.PriceAfterDiscount = returnEntity.Quantity > 0
                ? returnEntity.RefundAmount / returnEntity.Quantity
                : 0;
            detail.SubtotalBeforeTax = detail.PriceAfterDiscount * returnEntity.Quantity;
        }

        return detail;
    }

    public async Task<Common.PaginatedResult<ReturnDto>> GetPagedAsync(
        int pageNumber, int pageSize, DateTime? fromDate = null, DateTime? toDate = null)
    {
        // استخدام التصفية على مستوى قاعدة البيانات بدلاً من تحميل الكل
        var pagedSpec = new ReturnSpecification(pageNumber, pageSize, fromDate, toDate);
        var pagedReturns = await _unitOfWork.Returns.FindAsync(pagedSpec);

        // حساب العدد الإجمالي بدون تحميل البيانات
        var countSpec = new ReturnSpecification();
        var allMatching = await _unitOfWork.Returns.FindAsync(countSpec);

        // تطبيق فلتر التاريخ على العدد
        var filteredForCount = allMatching.AsEnumerable();
        if (fromDate.HasValue)
            filteredForCount = filteredForCount.Where(r => r.ReturnDate >= fromDate.Value.Date);
        if (toDate.HasValue)
            filteredForCount = filteredForCount.Where(r => r.ReturnDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var totalCount = filteredForCount.Count();
        var dtos = pagedReturns.Select(_mapper.Map<ReturnDto>).ToList();

        return new Common.PaginatedResult<ReturnDto>(dtos, totalCount, pageNumber, pageSize);
    }

    /// <summary>
    /// Helper: gets the total returned quantity for a specific spare part in a specific invoice.
    /// </summary>
    private async Task<int> GetReturnedQuantityAsync(int invoiceId, int sparePartId)
    {
        var spec = new ReturnSpecification(invoiceId, sparePartId);
        var returns = await _unitOfWork.Returns.FindAsync(spec);
        return returns.Sum(r => r.Quantity);
    }
}
