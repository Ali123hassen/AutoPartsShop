namespace AutoPartsShop.Application.DTOs.Invoices;

public class InvoiceItemDto
{
    public int SparePartId { get; set; }
    public string PartName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>
    /// تكلفة القطعة وقت البيع (لقطة محفوظة) - تُستخدم لحساب الأرباح بدقة
    /// </summary>
    public decimal CostAtSale { get; set; }

    /// <summary>
    /// رقم السطر في الفاتورة
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// خصم لكل وحدة (للعرض)
    /// </summary>
    public decimal DiscountAmountPerUnit => Quantity > 0 ? DiscountAmount / Quantity : 0;

    /// <summary>
    /// الكمية المرتجعة من هذا الصنف
    /// </summary>
    public int ReturnedQty { get; set; }

    /// <summary>
    /// الكمية المتبقية (الكمية - المرتجع)
    /// </summary>
    public int RemainingQty => Quantity - ReturnedQty;

    /// <summary>
    /// حالة الصنف: None / PartialReturn / FullReturn
    /// </summary>
    public string ItemReturnStatus { get; set; } = "None";
}
