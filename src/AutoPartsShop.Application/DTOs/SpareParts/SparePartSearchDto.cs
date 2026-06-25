namespace AutoPartsShop.Application.DTOs.SpareParts;

public class SparePartSearchDto
{
    public string? Keyword { get; set; }
    public string? PartNumber { get; set; }
    public string? Barcode { get; set; }
    public string? Location { get; set; }
    public int? CategoryId { get; set; }
    public bool? LowStockOnly { get; set; }
    public bool? IsActive { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}