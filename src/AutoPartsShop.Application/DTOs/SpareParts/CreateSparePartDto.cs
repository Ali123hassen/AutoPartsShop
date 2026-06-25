namespace AutoPartsShop.Application.DTOs.SpareParts;

/// <summary>
/// DTO لإنشاء قطعة غيار جديدة - بيانات كتالوجية فقط.
/// الأسعار والكميات والمورد تُدار عبر فواتير المشتريات.
/// </summary>
public class CreateSparePartDto
{
    public string PartNumber { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public int CategoryId { get; set; }
    public string? Manufacturer { get; set; }
    public string? Location { get; set; }
    public string Unit { get; set; } = "قطعة";
    public string? Notes { get; set; }
    public string? BarcodeType { get; set; }
    public string? BarcodeValue { get; set; }
    public string? CompatibleCar { get; set; }
    public string? CarModel { get; set; }
    public string? CarYear { get; set; }
    public string? CountryOfOrigin { get; set; }
    public decimal? Weight { get; set; }
}
