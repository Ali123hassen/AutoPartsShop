using AutoPartsShop.Core.Events;
using AutoPartsShop.Core.Exceptions;

namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a spare part / auto part available for sale.
/// Contains stock management logic and pricing information.
/// </summary>
public sealed class SparePart : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique part number (SKU).
    /// </summary>
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the barcode for scanning.
    /// </summary>
    public string Barcode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the part name (in English or primary language).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the part name in Arabic, if applicable.
    /// </summary>
    public string? NameAr { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the category this part belongs to.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the purchase (cost) price.
    /// </summary>
    public decimal PurchasePrice { get; set; }

    /// <summary>
    /// Gets or sets the selling price.
    /// </summary>
    public decimal SalePrice { get; set; }

    /// <summary>
    /// Gets or sets the current quantity in stock.
    /// </summary>
    public int CurrentStock { get; set; }

    /// <summary>
    /// Gets or sets the minimum stock level before a low-stock alert is raised.
    /// </summary>
    public int MinStockLevel { get; set; } = 5;

    /// <summary>
    /// Gets or sets the physical location/aisle of this part in the warehouse.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the unit of measure (defaults to "قطعة" meaning "piece").
    /// </summary>
    public string Unit { get; set; } = "قطعة";

    /// <summary>
    /// Gets or sets optional notes about this part.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets whether this part is active and available for sale.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the manufacturer/brand of the part.
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Gets or sets the minimum sale price (price floor).
    /// </summary>
    public decimal? MinSalePrice { get; set; }

    /// <summary>
    /// Gets or sets the maximum stock level (reorder point).
    /// </summary>
    public int? MaxStockLevel { get; set; }

    /// <summary>
    /// Gets or sets the supplier name.
    /// </summary>
    public string? SupplierName { get; set; }

    /// <summary>
    /// Gets or sets the supplier phone number.
    /// </summary>
    public string? SupplierPhone { get; set; }

    /// <summary>
    /// Gets or sets the date of the last purchase.
    /// </summary>
    public DateTime? LastPurchaseDate { get; set; }

    /// <summary>
    /// Gets or sets the barcode type (e.g., EAN-13, Code128, QR).
    /// </summary>
    public string? BarcodeType { get; set; }

    /// <summary>
    /// Gets or sets the barcode value (encoded data).
    /// </summary>
    public string? BarcodeValue { get; set; }

    /// <summary>
    /// Gets or sets the compatible car make/brand.
    /// </summary>
    public string? CompatibleCar { get; set; }

    /// <summary>
    /// Gets or sets the compatible car model.
    /// </summary>
    public string? CarModel { get; set; }

    /// <summary>
    /// Gets or sets the compatible car year.
    /// </summary>
    public string? CarYear { get; set; }

    /// <summary>
    /// Gets or sets the country of origin.
    /// </summary>
    public string? CountryOfOrigin { get; set; }

    /// <summary>
    /// Gets or sets the weight of the part in kg.
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this part was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // --- Navigation Properties ---

    /// <summary>
    /// Gets or sets the category this part belongs to.
    /// </summary>
    public Category Category { get; set; } = null!;

    /// <summary>
    /// Gets the collection of invoice items referencing this part.
    /// </summary>
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

    /// <summary>
    /// Gets the collection of stock movements for this part.
    /// </summary>
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    // --- Domain Events ---

    /// <summary>
    /// Gets the list of domain events raised by this entity.
    /// </summary>
    public List<IDomainEvent> DomainEvents { get; } = [];

    // --- Business Methods ---

    /// <summary>
    /// Gets whether this part is at or below the minimum stock level.
    /// </summary>
    public bool IsLowStock => CurrentStock <= MinStockLevel;

    /// <summary>
    /// Gets the profit margin percentage for this part.
    /// </summary>
    public decimal ProfitMargin => PurchasePrice == 0
        ? 100m
        : Math.Round(((SalePrice - PurchasePrice) / PurchasePrice) * 100m, 2);

    /// <summary>
    /// Deducts the specified quantity from current stock.
    /// </summary>
    /// <param name="quantity">The quantity to deduct. Must be positive.</param>
    /// <exception cref="InsufficientStockException">Thrown when current stock is less than the requested quantity.</exception>
    public void DeductStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity to deduct must be greater than zero.");

        if (CurrentStock < quantity)
            throw new InsufficientStockException(Name, CurrentStock, quantity);

        CurrentStock -= quantity;
        UpdatedAt = DateTime.UtcNow;

        if (IsLowStock)
            DomainEvents.Add(new LowStockEvent(Id, Name, CurrentStock, MinStockLevel));
    }

    /// <summary>
    /// Adds the specified quantity to current stock.
    /// </summary>
    /// <param name="quantity">The quantity to add. Must be positive.</param>
    /// <exception cref="DomainException">Thrown when quantity is not positive.</exception>
    public void AddStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity to add must be greater than zero.");

        CurrentStock += quantity;
        UpdatedAt = DateTime.UtcNow;
    }
}
