namespace AutoPartsShop.Application.DTOs.Reports;

public class StockReportDto
{
    public int TotalParts { get; set; }
    public int LowStockParts { get; set; }
    public int OutOfStockParts { get; set; }
    public decimal TotalStockValue { get; set; }
}
