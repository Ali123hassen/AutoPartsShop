using AutoMapper;
using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.DTOs.Stock;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Application.Specifications;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Enums;
using AutoPartsShop.Core.Exceptions;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Services;

public class StockService : IStockService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public StockService(
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

    public async Task AdjustStockAsync(StockAdjustmentDto dto)
    {
        var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(dto.SparePartId)
            ?? throw new DomainException($"Spare part with ID {dto.SparePartId} not found.");

        var previousStock = sparePart.CurrentStock;

        switch (dto.MovementType)
        {
            case MovementType.In:
                sparePart.AddStock(dto.Quantity);
                break;

            case MovementType.Out:
                if (sparePart.CurrentStock < dto.Quantity)
                    throw new InsufficientStockException(sparePart.Name, sparePart.CurrentStock, dto.Quantity);
                sparePart.DeductStock(dto.Quantity);
                break;

            case MovementType.Adjustment:
                // For adjustments, we set the stock to the specified quantity
                sparePart.CurrentStock = dto.Quantity;
                sparePart.UpdatedAt = DateTime.UtcNow;
                break;

            case MovementType.Return:
                sparePart.AddStock(dto.Quantity);
                break;

            default:
                throw new DomainException($"Unsupported movement type: {dto.MovementType}");
        }

        var newStock = sparePart.CurrentStock;

        await _unitOfWork.SpareParts.UpdateAsync(sparePart);

        // Record stock movement
        var movement = new StockMovement
        {
            SparePartId = dto.SparePartId,
            MovementType = dto.MovementType,
            Quantity = dto.Quantity,
            PreviousStock = previousStock,
            NewStock = newStock,
            ReferenceType = "ManualAdjustment",
            Notes = dto.Notes,
            UserId = GetCurrentUserId()
        };

        await _unitOfWork.StockMovements.AddAsync(movement);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("StockAdjustment", "SparePart", dto.SparePartId,
            $"Stock: {previousStock}", $"Stock: {newStock} ({dto.MovementType})");
    }

    public async Task<IReadOnlyList<SparePartDto>> GetLowStockAlertsAsync()
    {
        var allParts = await _unitOfWork.SpareParts.GetAllAsync();
        var lowStockParts = allParts.Where(sp => sp.IsLowStock).ToList();
        return lowStockParts.Select(_mapper.Map<SparePartDto>).ToList();
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(int sparePartId, int count = 50)
    {
        var spec = new StockMovementSpecification(sparePartId, count);
        var movements = await _unitOfWork.StockMovements.FindAsync(spec);

        return movements.Select(_mapper.Map<StockMovementDto>).ToList();
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetAllMovementsAsync(int count = 100)
    {
        var spec = new StockMovementSpecification(count);
        var movements = await _unitOfWork.StockMovements.FindAsync(spec);

        return movements.Select(_mapper.Map<StockMovementDto>).ToList();
    }

    public async Task<int> GetStockLevelAsync(int sparePartId)
    {
        var sparePart = await _unitOfWork.SpareParts.GetByIdAsync(sparePartId)
            ?? throw new DomainException($"Spare part with ID {sparePartId} not found.");

        return sparePart.CurrentStock;
    }
}
