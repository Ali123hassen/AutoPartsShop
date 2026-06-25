using AutoPartsShop.Core.Enums;

namespace AutoPartsShop.Application.DTOs.Stock;

public class StockAdjustmentDto
{
    public int SparePartId { get; set; }
    public MovementType MovementType { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}
