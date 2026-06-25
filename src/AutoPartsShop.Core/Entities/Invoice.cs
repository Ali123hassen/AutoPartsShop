using AutoPartsShop.Core.Enums;

namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a sales invoice for one or more spare parts.
/// </summary>
public sealed class Invoice : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique invoice number.
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time of the invoice.
    /// </summary>
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the foreign key to the user (cashier) who created this invoice.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Gets or sets the sub-total before discount.
    /// </summary>
    public decimal SubTotal { get; set; }

    /// <summary>
    /// Gets or sets the total discount amount applied.
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// Gets or sets the tax rate percentage used for this invoice.
    /// </summary>
    public decimal TaxRate { get; set; }

    /// <summary>
    /// Gets or sets the tax amount calculated for this invoice.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the total amount after discount and tax.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the amount paid by the customer.
    /// </summary>
    public decimal PaidAmount { get; set; }

    /// <summary>
    /// Gets or sets the change to return to the customer.
    /// </summary>
    public decimal ChangeAmount { get; set; }

    /// <summary>
    /// Gets or sets the payment method.
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets the invoice status.
    /// </summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Completed;

    /// <summary>
    /// Gets or sets the optional customer name.
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// Gets or sets optional notes about this invoice.
    /// </summary>
    public string? Notes { get; set; }

    // --- Navigation Properties ---

    /// <summary>
    /// Gets or sets the user (cashier) who created this invoice.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Gets the collection of line items in this invoice.
    /// </summary>
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();

    /// <summary>
    /// Gets the collection of returns associated with this invoice.
    /// </summary>
    public ICollection<Return> Returns { get; set; } = new List<Return>();

    // --- Business Methods ---

    /// <summary>
    /// Recalculates SubTotal, TaxAmount, TotalAmount, and ChangeAmount from the line items.
    /// Note: Per-item discounts are already applied in LineTotal, so DiscountAmount is
    /// recorded for reference only and should NOT be subtracted again from TotalAmount.
    /// </summary>
    public void CalculateTotals()
    {
        SubTotal = Items.Sum(i => i.LineTotal);
        TaxAmount = SubTotal * (TaxRate / 100m);
        TotalAmount = SubTotal + TaxAmount;
        if (TotalAmount < 0) TotalAmount = 0;
        ChangeAmount = PaidAmount - TotalAmount;
        if (ChangeAmount < 0) ChangeAmount = 0;
    }

    /// <summary>
    /// Cancels this invoice by setting its status to Cancelled.
    /// </summary>
    /// <exception cref="Exceptions.DomainException">Thrown when the invoice is already cancelled.</exception>
    public void Cancel()
    {
        if (Status == InvoiceStatus.Cancelled)
            throw new Exceptions.DomainException("Invoice is already cancelled.");

        Status = InvoiceStatus.Cancelled;
    }
}
