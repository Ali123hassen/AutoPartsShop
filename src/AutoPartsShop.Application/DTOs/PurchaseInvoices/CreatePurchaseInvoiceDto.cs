namespace AutoPartsShop.Application.DTOs.PurchaseInvoices;

public class CreatePurchaseInvoiceDto
{
    public List<PurchaseInvoiceItemDto> Items { get; set; } = [];
    public string? SupplierName { get; set; }
    public string? SupplierPhone { get; set; }
    public string? Notes { get; set; }
}
