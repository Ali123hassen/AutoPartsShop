namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a single line item within a purchase invoice.
/// </summary>
public sealed class PurchaseInvoiceItem : BaseEntity
{
    /// <summary>
    /// Gets or sets the foreign key to the parent purchase invoice.
    /// </summary>
    public int PurchaseInvoiceId { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the spare part being purchased.
    /// </summary>
    public int SparePartId { get; set; }

    /// <summary>
    /// Gets or sets the name of the part at the time of purchase (snapshot).
    /// </summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity purchased.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the cost price per unit at the time of purchase.
    /// </summary>
    public decimal CostPrice { get; set; }

    /// <summary>
    /// Gets or sets the selling price per unit (to update on SparePart).
    /// </summary>
    public decimal SalePrice { get; set; }

    /// <summary>
    /// Gets or sets the minimum sale price per unit (to update on SparePart).
    /// </summary>
    public decimal? MinSalePrice { get; set; }

    /// <summary>
    /// Gets or sets the total amount for this line item (Quantity * CostPrice).
    /// </summary>
    public decimal LineTotal { get; set; }

    /// <summary>
    /// لقطة من المخزون قبل إضافة هذه الدفعة.
    /// تُستخدم عند إلغاء فاتورة الشراء لحساب متوسط التكلفة بدقة.
    /// </summary>
    public int PreviousStock { get; set; }

    /// <summary>
    /// لقطة من متوسط التكلفة قبل إضافة هذه الدفعة.
    /// عند الإلغاء، نعيد التكلفة لهذه القيمة مباشرة بدلاً من الحساب العكسي
    /// الذي يفشل بعد عمليات شراء لاحقة.
    /// </summary>
    public decimal PreviousCostPrice { get; set; }

    // --- Navigation Properties ---

    /// <summary>
    /// Gets or sets the parent purchase invoice.
    /// </summary>
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;

    /// <summary>
    /// Gets or sets the spare part being purchased.
    /// </summary>
    public SparePart SparePart { get; set; } = null!;
}
