namespace AutoPartsShop.Application.DTOs.PurchaseInvoices;

public class PurchaseInvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string? SupplierPhone { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
    public List<PurchaseInvoiceItemDto> Items { get; set; } = [];
}
