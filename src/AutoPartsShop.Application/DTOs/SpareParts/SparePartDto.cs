namespace AutoPartsShop.Application.DTOs.SpareParts;

public class SparePartDto
{
    public int Id { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Manufacturer { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? MinSalePrice { get; set; }
    public int CurrentStock { get; set; }
    public int MinStockLevel { get; set; }
    public int? MaxStockLevel { get; set; }
    public string? Location { get; set; }
    public string Unit { get; set; } = "قطعة";
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierPhone { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public string? BarcodeType { get; set; }
    public string? BarcodeValue { get; set; }
    public string? CompatibleCar { get; set; }
    public string? CarModel { get; set; }
    public string? CarYear { get; set; }
    public string? CountryOfOrigin { get; set; }
    public decimal? Weight { get; set; }
    public bool IsLowStock { get; set; }
    public decimal ProfitMargin { get; set; }
}