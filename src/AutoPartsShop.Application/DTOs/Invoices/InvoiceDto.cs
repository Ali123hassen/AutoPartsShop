namespace AutoPartsShop.Application.DTOs.Invoices;

public class InvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string UserName { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? Notes { get; set; }
    public List<InvoiceItemDto> Items { get; set; } = [];

    /// <summary>
    /// حالة المرتجع: None / Partial / Full
    /// </summary>
    public string ReturnStatus { get; set; } = "None";

    // ===== تفاصيل المرتجع المالي =====

    /// <summary>
    /// الإجمالي الفرعي للمرتجع (قبل الخصم) = مجموع (الكمية المرتجعة × سعر الوحدة)
    /// </summary>
    public decimal ReturnSubTotal { get; set; }

    /// <summary>
    /// خصم المرتجع = الإجمالي الفرعي - بعد الخصم
    /// </summary>
    public decimal ReturnDiscount { get; set; }

    /// <summary>
    /// المرتجع بعد الخصم (قبل الضريبة) = مجموع (الكمية المرتجعة × سعر الوحدة بعد الخصم)
    /// </summary>
    public decimal ReturnAfterDiscount { get; set; }

    /// <summary>
    /// ضريبة المرتجع = المرتجع بعد الخصم × نسبة الضريبة
    /// </summary>
    public decimal ReturnTax { get; set; }

    /// <summary>
    /// إجمالي المرتجع شامل الضريبة = المرتجع بعد الخصم + ضريبة المرتجع
    /// </summary>
    public decimal ReturnTotal { get; set; }
}
