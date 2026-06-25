namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a single line item within an invoice.
/// </summary>
public sealed class InvoiceItem : BaseEntity
{
    /// <summary>
    /// Gets or sets the foreign key to the parent invoice.
    /// </summary>
    public int InvoiceId { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the spare part being sold.
    /// </summary>
    public int SparePartId { get; set; }

    /// <summary>
    /// Gets or sets the name of the part at the time of sale (snapshot).
    /// </summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity sold.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the unit price at the time of sale.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the discount percentage applied to this line item.
    /// Stored for reference, but DiscountAmount is used for exact calculations.
    /// </summary>
    public decimal DiscountPercent { get; set; } = 0;

    /// <summary>
    /// Gets or sets the total discount amount for the entire line (not per unit).
    /// This is the exact amount the user entered, avoiding rounding errors from DiscountPercent.
    /// For example, if Quantity=3, UnitPrice=16000, DiscountAmount=1000, the customer pays 47000.
    /// </summary>
    public decimal DiscountAmount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the total amount for this line item after discount.
    /// </summary>
    public decimal LineTotal { get; set; }

    /// <summary>
    /// Gets or sets the cost price of the part at the time of sale (snapshot).
    /// This ensures historical profit calculations remain stable even when
    /// the current cost price changes due to new purchases at different prices.
    /// </summary>
    public decimal CostAtSale { get; set; }

    // --- Navigation Properties ---

    /// <summary>
    /// Gets or sets the parent invoice.
    /// </summary>
    public Invoice Invoice { get; set; } = null!;

    /// <summary>
    /// Gets or sets the spare part referenced by this line item.
    /// </summary>
    public SparePart SparePart { get; set; } = null!;
}
