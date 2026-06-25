using AutoPartsShop.Core.Enums;

namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a purchase invoice for buying spare parts from suppliers.
/// When completed, stock is added to the inventory.
/// </summary>
public sealed class PurchaseInvoice : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique purchase invoice number.
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time of the purchase.
    /// </summary>
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the foreign key to the user who created this purchase invoice.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Gets or sets the name of the supplier.
    /// </summary>
    public string? SupplierName { get; set; }

    /// <summary>
    /// Gets or sets the phone number of the supplier.
    /// </summary>
    public string? SupplierPhone { get; set; }

    /// <summary>
    /// Gets or sets the total amount of the purchase invoice.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets any notes about this purchase invoice.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the status of the purchase invoice.
    /// </summary>
    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Completed;

    // --- Navigation Properties ---

    /// <summary>
    /// Gets or sets the user who created this purchase invoice.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Gets the collection of line items in this purchase invoice.
    /// </summary>
    public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();

    // --- Business Methods ---

    /// <summary>
    /// Recalculates TotalAmount from the line items.
    /// </summary>
    public void CalculateTotals()
    {
        TotalAmount = Items.Sum(i => i.LineTotal);
    }

    /// <summary>
    /// Cancels this purchase invoice by setting its status to Cancelled.
    /// </summary>
    /// <exception cref="Exceptions.DomainException">Thrown when the invoice is already cancelled.</exception>
    public void Cancel()
    {
        if (Status == PurchaseInvoiceStatus.Cancelled)
            throw new Exceptions.DomainException("Purchase invoice is already cancelled.");

        Status = PurchaseInvoiceStatus.Cancelled;
    }
}
