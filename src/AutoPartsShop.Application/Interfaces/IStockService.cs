using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.DTOs.Stock;

namespace AutoPartsShop.Application.Interfaces;

public interface IStockService
{
    Task AdjustStockAsync(StockAdjustmentDto dto);
    Task<IReadOnlyList<SparePartDto>> GetLowStockAlertsAsync();
    Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(int sparePartId, int count = 50);
    Task<IReadOnlyList<StockMovementDto>> GetAllMovementsAsync(int count = 100);
    Task<int> GetStockLevelAsync(int sparePartId);
}
