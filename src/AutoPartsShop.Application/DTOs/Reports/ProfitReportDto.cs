namespace AutoPartsShop.Application.DTOs.Reports;

public class ProfitReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// إجمالي الإيرادات بعد الخصم وقبل الضريبة (SubTotal)
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// إجمالي الإيرادات قبل الضريبة وقبل الخصم
    /// = Sum of (Quantity × UnitPrice) for all items
    /// </summary>
    public decimal TotalRevenueBeforeDiscount { get; set; }

    /// <summary>
    /// إجمالي الخصومات على مستوى الأصناف (من InvoiceItems.DiscountAmount)
    /// </summary>
    public decimal TotalItemDiscounts { get; set; }

    /// <summary>
    /// إجمالي الخصومات على مستوى الفاتورة (من Invoice.DiscountAmount)
    /// لا يُستخدم حالياً لأن خصم الفاتورة = مجموع خصوم الأصناف
    /// </summary>
    public decimal TotalInvoiceDiscounts { get; set; }

    /// <summary>
    /// إجمالي جميع الخصومات = TotalItemDiscounts
    /// </summary>
    public decimal TotalDiscounts => TotalItemDiscounts;

    /// <summary>
    /// إجمالي الضريبة المحصلة (على كل المبيعات)
    /// </summary>
    public decimal TotalTax { get; set; }

    /// <summary>
    /// صافي الضريبة = إجمالي الضريبة - ضريبة المرتجعات
    /// </summary>
    public decimal NetTax { get; set; }

    /// <summary>
    /// إجمالي الإيرادات بعد الضريبة (TotalAmount) - للعرض فقط
    /// </summary>
    public decimal TotalRevenueWithTax { get; set; }

    /// <summary>
    /// إجمالي تكلفة الشراء (لكل أصناف الفواتير)
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// إجمالي المرتجعات قبل الضريبة (مبلغ الأصناف بعد الخصم بدون ضريبة)
    /// </summary>
    public decimal TotalReturnsBeforeTax { get; set; }

    /// <summary>
    /// إجمالي المرتجعات قبل الخصم (سعر الوحدة الأصلي × الكمية المرتجعة)
    /// </summary>
    public decimal ReturnsBeforeDiscount { get; set; }

    /// <summary>
    /// ضريبة المرتجعات (الضريبة المسترجعة للعملاء)
    /// </summary>
    public decimal ReturnsTax { get; set; }

    /// <summary>
    /// إجمالي المرتجعات شامل الضريبة (المبلغ الفعلي المسترجع للعملاء)
    /// </summary>
    public decimal TotalReturns { get; set; }

    /// <summary>
    /// تكلفة الأصناف المرتجعة (CostAtSale × ReturnedQuantity)
    /// </summary>
    public decimal ReturnedCost { get; set; }

    /// <summary>
    /// صافي الإيرادات = الإيرادات بعد الخصم - المرتجعات قبل الضريبة
    /// </summary>
    public decimal NetRevenue { get; set; }

    /// <summary>
    /// صافي التكاليف = التكاليف - تكلفة المرتجعات
    /// </summary>
    public decimal NetCost { get; set; }

    /// <summary>
    /// صافي الربح = صافي الإيرادات - صافي التكاليف
    /// </summary>
    public decimal RealNetProfit { get; set; }

    /// <summary>
    /// هامش الربح % = (صافي الربح / صافي الإيرادات) × 100
    /// </summary>
    public decimal ProfitMargin { get; set; }

    public int TotalItemsSold { get; set; }
}
