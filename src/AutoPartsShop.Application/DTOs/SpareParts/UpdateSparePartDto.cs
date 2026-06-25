namespace AutoPartsShop.Application.DTOs.SpareParts;

/// <summary>
/// DTO لتحديث قطعة غيار - بيانات كتالوجية فقط.
/// سعر الشراء والمخزون والمورد تُدار عبر فواتير المشتريات ولا يمكن تعديلها من هنا.
/// </summary>
public class UpdateSparePartDto
{
    public int Id { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public int CategoryId { get; set; }
    public string? Manufacturer { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? MinSalePrice { get; set; }
    public int MinStockLevel { get; set; } = 5;
    public int? MaxStockLevel { get; set; }
    public string? Location { get; set; }
    public string Unit { get; set; } = "قطعة";
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public string? BarcodeType { get; set; }
    public string? BarcodeValue { get; set; }
    public string? CompatibleCar { get; set; }
    public string? CarModel { get; set; }
    public string? CarYear { get; set; }
    public string? CountryOfOrigin { get; set; }
    public decimal? Weight { get; set; }
}
