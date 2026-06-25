namespace AutoPartsShop.Core.Events;

/// <summary>
/// Raised when a spare part's stock falls to or below its minimum stock level.
/// </summary>
public sealed class LowStockEvent : IDomainEvent
{
    /// <summary>
    /// Gets the identifier of the spare part with low stock.
    /// </summary>
    public int SparePartId { get; }

    /// <summary>
    /// Gets the name of the spare part with low stock.
    /// </summary>
    public string PartName { get; }

    /// <summary>
    /// Gets the current stock quantity.
    /// </summary>
    public int CurrentStock { get; }

    /// <summary>
    /// Gets the minimum stock level threshold.
    /// </summary>
    public int MinStockLevel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LowStockEvent"/> class.
    /// </summary>
    /// <param name="sparePartId">The spare part identifier.</param>
    /// <param name="partName">The spare part name.</param>
    /// <param name="currentStock">The current stock quantity.</param>
    /// <param name="minStockLevel">The minimum stock level threshold.</param>
    public LowStockEvent(int sparePartId, string partName, int currentStock, int minStockLevel)
    {
        SparePartId = sparePartId;
        PartName = partName;
        CurrentStock = currentStock;
        MinStockLevel = minStockLevel;
    }
}
