namespace AutoPartsShop.Application.DTOs.Reports;

public class DailySalesReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalInvoices { get; set; }

    /// <summary>
    /// إجمالي المبيعات قبل الخصم وقبل الضريبة = Sum of (Quantity × UnitPrice)
    /// </summary>
    public decimal TotalSalesBeforeDiscount { get; set; }

    /// <summary>
    /// إجمالي الخصومات (مجموع خصومات الأصناف)
    /// </summary>
    public decimal TotalDiscounts { get; set; }

    /// <summary>
    /// إجمالي المبيعات بعد الخصم وقبل الضريبة (SubTotal)
    /// </summary>
    public decimal TotalSales { get; set; }

    /// <summary>
    /// إجمالي الضريبة المحصلة
    /// </summary>
    public decimal TotalTax { get; set; }

    /// <summary>
    /// إجمالي المبيعات بعد الضريبة (TotalAmount) - للعرض فقط
    /// </summary>
    public decimal TotalSalesWithTax { get; set; }

    /// <summary>
    /// إجمالي المرتجعات قبل الضريبة (مبلغ الأصناف المسترجعة بعد الخصم بدون الضريبة)
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
    /// صافي المبيعات قبل الضريبة = المبيعات بعد الخصم - المرتجعات قبل الضريبة
    /// </summary>
    public decimal NetSales { get; set; }

    /// <summary>
    /// صافي الضريبة = إجمالي الضريبة - ضريبة المرتجعات
    /// </summary>
    public decimal NetTax { get; set; }

    /// <summary>
    /// إجمالي تكلفة الشراء (لكل أصناف الفواتير)
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// تكلفة الأصناف المرتجعة (CostAtSale × ReturnedQuantity)
    /// </summary>
    public decimal ReturnedCost { get; set; }

    /// <summary>
    /// صافي التكاليف = التكاليف - تكلفة المرتجعات
    /// </summary>
    public decimal NetCost { get; set; }

    /// <summary>
    /// صافي الربح = صافي المبيعات - صافي التكاليف
    /// </summary>
    public decimal TotalProfit { get; set; }
}
