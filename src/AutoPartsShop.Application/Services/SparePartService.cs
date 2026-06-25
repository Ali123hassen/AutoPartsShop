using AutoMapper;
using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Application.Specifications;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Services;

public class SparePartService : ISparePartService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;
    private readonly IBarcodeService _barcodeService;

    public SparePartService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IAuditService auditService,
        IBarcodeService barcodeService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _auditService = auditService;
        _barcodeService = barcodeService;
    }

    public async Task<SparePartDto> CreateAsync(CreateSparePartDto dto)
    {
        var existingByPartNumber = await GetByPartNumberAsync(dto.PartNumber);
        if (existingByPartNumber is not null)
            throw new Core.Exceptions.DomainException($"A spare part with part number '{dto.PartNumber}' already exists.");

        var existingByBarcode = await GetByBarcodeAsync(dto.Barcode);
        if (existingByBarcode is not null)
            throw new Core.Exceptions.DomainException($"A spare part with barcode '{dto.Barcode}' already exists.");

        var sparePart = _mapper.Map<Core.Entities.SparePart>(dto);

        if (string.IsNullOrWhiteSpace(sparePart.Barcode) || sparePart.Barcode == "0")
        {
            sparePart.Barcode = _barcodeService.GenerateBarcode();
        }

        var added = await _unitOfWork.SpareParts.AddAsync(sparePart);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Create", "SparePart", added.Id, null, $"Created: {added.Name} ({added.PartNumber})");

        var fetched = await _unitOfWork.SpareParts.GetByIdAsync(added.Id);
        return _mapper.Map<SparePartDto>(fetched!);
    }

    public async Task<SparePartDto> UpdateAsync(UpdateSparePartDto dto)
    {
        var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(dto.Id)
            ?? throw new Core.Exceptions.DomainException($"Spare part with ID {dto.Id} not found.");

        var spec = new SparePartSpecification(new SparePartSearchDto { PartNumber = dto.PartNumber });
        var duplicates = await _unitOfWork.SpareParts.FindAsync(spec);
        if (duplicates.Any(d => d.Id != dto.Id))
            throw new Core.Exceptions.DomainException($"Another spare part with part number '{dto.PartNumber}' already exists.");

        var oldValues = $"Name: {sparePart.Name}, PartNumber: {sparePart.PartNumber}, SalePrice: {sparePart.SalePrice}";

        _mapper.Map(dto, sparePart);
        sparePart.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SpareParts.UpdateAsync(sparePart);
        await _unitOfWork.SaveChangesAsync();

        var newValues = $"Name: {sparePart.Name}, PartNumber: {sparePart.PartNumber}, SalePrice: {sparePart.SalePrice}";
        await _auditService.LogAsync("Update", "SparePart", sparePart.Id, oldValues, newValues);

        var fetched = await _unitOfWork.SpareParts.GetByIdAsync(sparePart.Id);
        return _mapper.Map<SparePartDto>(fetched!);
    }

    public async Task DeleteAsync(int id)
    {
        var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(id)
            ?? throw new Core.Exceptions.DomainException($"Spare part with ID {id} not found.");

        // التحقق من عدم وجود فواتير مرتبطة بهذه القطعة
        var invoiceItems = await _unitOfWork.InvoiceItems.GetAllAsync();
        var hasInvoiceItems = invoiceItems.Any(ii => ii.SparePartId == id);
        if (hasInvoiceItems)
            throw new Core.Exceptions.DomainException("لا يمكن حذف هذه القطعة لأنها مرتبطة بفواتير بيع. يمكنك تعطيلها بدلاً من حذفها.");

        // التحقق من عدم وجود فواتير شراء مرتبطة
        var purchaseInvoiceItems = await _unitOfWork.PurchaseInvoiceItems.GetAllAsync();
        var hasPurchaseInvoiceItems = purchaseInvoiceItems.Any(ii => ii.SparePartId == id);
        if (hasPurchaseInvoiceItems)
            throw new Core.Exceptions.DomainException("لا يمكن حذف هذه القطعة لأنها مرتبطة بفواتير شراء. يمكنك تعطيلها بدلاً من حذفها.");

        // التحقق من عدم وجود مرتجعات مرتبطة
        var returns = await _unitOfWork.Returns.GetAllAsync();
        var hasReturns = returns.Any(r => r.SparePartId == id || r.ReplacementPartId == id);
        if (hasReturns)
            throw new Core.Exceptions.DomainException("لا يمكن حذف هذه القطعة لأنها مرتبطة بمرتجعات. يمكنك تعطيلها بدلاً من حذفها.");

        // التحقق من عدم وجود حركات مخزون مرتبطة
        var stockMovements = await _unitOfWork.StockMovements.GetAllAsync();
        var hasStockMovements = stockMovements.Any(sm => sm.SparePartId == id);
        if (hasStockMovements)
            throw new Core.Exceptions.DomainException("لا يمكن حذف هذه القطعة لأنها مرتبطة بحركات مخزون. يمكنك تعطيلها بدلاً من حذفها.");

        var oldValues = $"Deleted: {sparePart.Name} ({sparePart.PartNumber})";

        await _unitOfWork.SpareParts.DeleteAsync(sparePart);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Delete", "SparePart", id, oldValues, null);
    }

    public async Task<SparePartDto?> GetByIdAsync(int id)
    {
        var spec = new SparePartSpecification(new SparePartSearchDto());
        var allWithCategory = await _unitOfWork.SpareParts.FindAsync(spec);
        var sparePart = allWithCategory.FirstOrDefault(sp => sp.Id == id);

        if (sparePart is null)
        {
            var direct = await _unitOfWork.SpareParts.GetByIdAsync(id);
            return direct is null ? null : _mapper.Map<SparePartDto>(direct);
        }

        return _mapper.Map<SparePartDto>(sparePart);
    }

    public async Task<SparePartDto?> GetByBarcodeAsync(string barcode)
    {
        var spec = new SparePartSpecification(new SparePartSearchDto { Barcode = barcode });
        var results = await _unitOfWork.SpareParts.FindAsync(spec);
        var sparePart = results.FirstOrDefault();

        return sparePart is null ? null : _mapper.Map<SparePartDto>(sparePart);
    }

    public async Task<SparePartDto?> GetByPartNumberAsync(string partNumber)
    {
        var spec = new SparePartSpecification(new SparePartSearchDto { PartNumber = partNumber });
        var results = await _unitOfWork.SpareParts.FindAsync(spec);
        var sparePart = results.FirstOrDefault();

        return sparePart is null ? null : _mapper.Map<SparePartDto>(sparePart);
    }

    public async Task<Common.PaginatedResult<SparePartDto>> SearchAsync(SparePartSearchDto search)
    {
        var countSearch = new SparePartSearchDto
        {
            Keyword = search.Keyword,
            PartNumber = search.PartNumber,
            Barcode = search.Barcode,
            Location = search.Location,
            CategoryId = search.CategoryId,
            LowStockOnly = search.LowStockOnly,
            PageNumber = 1,
            PageSize = int.MaxValue
        };
        var countSpec = new SparePartSpecification(countSearch);
        var allMatching = await _unitOfWork.SpareParts.FindAsync(countSpec);
        var totalCount = allMatching.Count;

        var spec = new SparePartSpecification(search);
        var results = await _unitOfWork.SpareParts.FindAsync(spec);
        var dtos = results.Select(_mapper.Map<SparePartDto>).ToList();

        return new Common.PaginatedResult<SparePartDto>(dtos, totalCount, search.PageNumber, search.PageSize);
    }

    public async Task<IReadOnlyList<SparePartDto>> GetLowStockAsync()
    {
        var spec = new SparePartSpecification(lowStockOnly: true);
        var results = await _unitOfWork.SpareParts.FindAsync(spec);
        return results.Select(_mapper.Map<SparePartDto>).ToList();
    }

    public async Task<IReadOnlyList<SparePartDto>> GetAllAsync()
    {
        var spec = new SparePartSpecification(new SparePartSearchDto { PageSize = int.MaxValue });
        var results = await _unitOfWork.SpareParts.FindAsync(spec);
        return results.Select(_mapper.Map<SparePartDto>).ToList();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync()
    {
        var categories = await _unitOfWork.Categories.GetAllAsync();
        return _mapper.Map<IReadOnlyList<CategoryDto>>(categories);
    }
}