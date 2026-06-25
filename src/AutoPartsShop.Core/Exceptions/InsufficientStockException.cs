namespace AutoPartsShop.Core.Exceptions;

/// <summary>
/// Thrown when attempting to sell or deduct more stock than is currently available.
/// </summary>
public sealed class InsufficientStockException : DomainException
{
    /// <summary>
    /// Gets the name of the spare part with insufficient stock.
    /// </summary>
    public string PartName { get; }

    /// <summary>
    /// Gets the current available stock quantity.
    /// </summary>
    public int CurrentStock { get; }

    /// <summary>
    /// Gets the quantity that was requested.
    /// </summary>
    public int RequestedQuantity { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InsufficientStockException"/> class.
    /// </summary>
    /// <param name="partName">The name of the spare part.</param>
    /// <param name="currentStock">The current stock quantity.</param>
    /// <param name="requestedQuantity">The requested quantity.</param>
    public InsufficientStockException(string partName, int currentStock, int requestedQuantity)
        : base($"Insufficient stock for '{partName}'. Available: {currentStock}, Requested: {requestedQuantity}.")
    {
        PartName = partName;
        CurrentStock = currentStock;
        RequestedQuantity = requestedQuantity;
    }
}
