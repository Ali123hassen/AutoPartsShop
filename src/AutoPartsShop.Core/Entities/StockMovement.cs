using AutoPartsShop.Core.Enums;

namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a stock movement record — tracks every change in inventory quantity.
/// </summary>
public sealed class StockMovement : BaseEntity
{
    /// <summary>
    /// Gets or sets the foreign key to the spare part affected by this movement.
    /// </summary>
    public int SparePartId { get; set; }

    /// <summary>
    /// Gets or sets the type of stock movement (in, out, return, adjustment).
    /// </summary>
    public MovementType MovementType { get; set; }

    /// <summary>
    /// Gets or sets the quantity moved (positive number; direction is implied by <see cref="MovementType"/>).
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the stock level before this movement.
    /// </summary>
    public int PreviousStock { get; set; }

    /// <summary>
    /// Gets or sets the stock level after this movement.
    /// </summary>
    public int NewStock { get; set; }

    /// <summary>
    /// Gets or sets the type of entity that triggered this movement (e.g., "Invoice", "Return").
    /// </summary>
    public string? ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the referencing entity, if applicable.
    /// </summary>
    public int? ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets optional notes about this movement.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the user who triggered this movement.
    /// </summary>
    public int UserId { get; set; }

    // --- Navigation Properties ---

    /// <summary>
    /// Gets or sets the spare part affected by this movement.
    /// </summary>
    public SparePart SparePart { get; set; } = null!;

    /// <summary>
    /// Gets or sets the user who triggered this movement.
    /// </summary>
    public User User { get; set; } = null!;
}
