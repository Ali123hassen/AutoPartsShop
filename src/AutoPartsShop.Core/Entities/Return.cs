using AutoPartsShop.Core.Enums;

namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a return transaction — either a refund or an exchange.
/// </summary>
public sealed class Return : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique return number.
    /// </summary>
    public string ReturnNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the foreign key to the original invoice, if applicable.
    /// </summary>
    public int? InvoiceId { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the return.
    /// </summary>
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the type of return (refund or exchange).
    /// </summary>
    public ReturnType ReturnType { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the spare part being returned.
    /// </summary>
    public int SparePartId { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the replacement part (for exchanges), if applicable.
    /// </summary>
    public int? ReplacementPartId { get; set; }

    /// <summary>
    /// Gets or sets the quantity being returned.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the monetary amount to be refunded.
    /// </summary>
    public decimal RefundAmount { get; set; }

    /// <summary>
    /// Gets or sets the reason for the return.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the user who processed the return.
    /// </summary>
    public int UserId { get; set; }

    // --- Navigation Properties ---

    /// <summary>
    /// Gets or sets the original invoice, if the return is linked to one.
    /// </summary>
    public Invoice? Invoice { get; set; }

    /// <summary>
    /// Gets or sets the spare part being returned.
    /// </summary>
    public SparePart SparePart { get; set; } = null!;

    /// <summary>
    /// Gets or sets the replacement spare part (for exchanges).
    /// </summary>
    public SparePart? ReplacementPart { get; set; }

    /// <summary>
    /// Gets or sets the user who processed the return.
    /// </summary>
    public User User { get; set; } = null!;
}
