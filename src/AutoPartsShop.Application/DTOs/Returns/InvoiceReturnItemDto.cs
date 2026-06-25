namespace AutoPartsShop.Application.DTOs.Returns;

/// <summary>
/// Represents an invoice line item with return tracking information.
/// Used to display available-for-return items when processing a return.
/// </summary>
public class InvoiceReturnItemDto
{
    /// <summary>Foreign key to the spare part.</summary>
    public int SparePartId { get; set; }

    /// <summary>Name of the part at the time of sale.</summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>Part number / SKU.</summary>
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>Quantity originally sold in the invoice.</summary>
    public int SoldQuantity { get; set; }

    /// <summary>Quantity that has already been returned for this item in this invoice.</summary>
    public int PreviouslyReturnedQuantity { get; set; }

    /// <summary>Quantity still available for return: SoldQuantity - PreviouslyReturnedQuantity.</summary>
    public int AvailableToReturn => SoldQuantity - PreviouslyReturnedQuantity;

    /// <summary>Unit price at the time of sale.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Discount percentage on this line item.</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Total discount amount for the entire line (exact value, not recalculated from percentage).</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Total line amount after discount.</summary>
    public decimal LineTotal { get; set; }

    /// <summary>Tax rate applied to the invoice (e.g., 5 for 5%).</summary>
    public decimal TaxRate { get; set; }
}
