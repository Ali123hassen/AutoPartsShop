namespace AutoPartsShop.Application.DTOs.PurchaseInvoices;

public class PurchaseInvoiceItemDto
{
    public int SparePartId { get; set; }
    public string PartName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? MinSalePrice { get; set; }
    public decimal LineTotal { get; set; }
}
