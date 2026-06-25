namespace AutoPartsShop.Application.DTOs.Reports;

public class TopSellingPartDto
{
    public int SparePartId { get; set; }
    public string PartName { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>
    /// سعر الوحدة الأساسي (قبل الخصم)
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// إجمالي الكمية المباعة (قبل خصم المرتجعات)
    /// </summary>
    public int TotalQuantitySold { get; set; }

    /// <summary>
    /// الكمية المرتجعة
    /// </summary>
    public int ReturnedQuantity { get; set; }

    /// <summary>
    /// صافي الكمية المباعة = TotalQuantitySold - ReturnedQuantity
    /// </summary>
    public int NetQuantitySold => TotalQuantitySold - ReturnedQuantity;

    /// <summary>
    /// إجمالي الإيرادات قبل خصم المرتجعات (بعد خصم الفاتورة على المبيعات)
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// إجمالي الخصميات على القطع المباعة (من الفواتير المكتملة والمرتجع جزئياً)
    /// </summary>
    public decimal TotalDiscount { get; set; }

    /// <summary>
    /// إيرادات المرتجعات - محسوبة من الفاتورة الأصلية لكل مرتجع
    /// كل قطعة مرتجعة تُحسب بسعرها الفعلي بعد خصمها الخاص من فاتورتها
    /// </summary>
    public decimal ReturnedRevenue { get; set; }

    /// <summary>
    /// صافي الإيرادات = TotalRevenue - ReturnedRevenue
    /// </summary>
    public decimal NetRevenue => TotalRevenue - ReturnedRevenue;
}
