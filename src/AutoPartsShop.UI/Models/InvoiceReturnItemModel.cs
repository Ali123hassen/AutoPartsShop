using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPartsShop.UI.Models;

/// <summary>
/// UI model for an invoice item displayed in the returns dialog.
/// Supports selection, return quantity entry, and auto-calculation of refund including tax.
/// </summary>
public partial class InvoiceReturnItemModel : ObservableObject
{
    /// <summary>Foreign key to the spare part.</summary>
    public int SparePartId { get; set; }

    /// <summary>Name of the part.</summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>Part number / SKU.</summary>
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>Quantity originally sold in the invoice.</summary>
    public int SoldQuantity { get; set; }

    /// <summary>Quantity that has already been returned.</summary>
    public int PreviouslyReturnedQuantity { get; set; }

    /// <summary>Quantity still available for return.</summary>
    public int AvailableToReturn => SoldQuantity - PreviouslyReturnedQuantity;

    /// <summary>Unit price at the time of sale.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Discount percentage on this line item.</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Total discount amount for the entire line (exact value from the invoice, not recalculated).</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Total line amount after discount.</summary>
    public decimal LineTotal { get; set; }

    /// <summary>Tax rate applied to the invoice (e.g., 5 for 5%).</summary>
    public decimal TaxRate { get; set; }

    /// <summary>Whether this item is selected for return.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Quantity the user wants to return.</summary>
    [ObservableProperty]
    private int _returnQuantity;

    /// <summary>
    /// Called when ReturnQuantity changes — clamps the value to valid range.
    /// </summary>
    partial void OnReturnQuantityChanged(int value)
    {
        if (value > AvailableToReturn)
            ReturnQuantity = AvailableToReturn;
        else if (value < 0)
            ReturnQuantity = 0;
    }

    /// <summary>
    /// Called when IsSelected changes — auto-set ReturnQuantity to 1 when selected, 0 when deselected.
    /// </summary>
    partial void OnIsSelectedChanged(bool value)
    {
        if (value && ReturnQuantity == 0)
            ReturnQuantity = 1;
        else if (!value)
            ReturnQuantity = 0;
    }

    /// <summary>
    /// Price per unit after discount (without tax).
    /// </summary>
    public decimal PricePerUnit => SoldQuantity > 0 ? LineTotal / SoldQuantity : 0;

    /// <summary>
    /// Discount amount per unit: DiscountAmount / SoldQuantity.
    /// Uses the exact stored DiscountAmount instead of recalculating from percentage.
    /// </summary>
    public decimal DiscountAmountPerUnit => SoldQuantity > 0 ? DiscountAmount / SoldQuantity : 0;

    /// <summary>
    /// Total discount amount for the returned quantity.
    /// Uses the exact stored DiscountAmount proportionally.
    /// </summary>
    public decimal CalculatedDiscountAmount
    {
        get
        {
            if (ReturnQuantity <= 0 || DiscountAmount <= 0) return 0;
            // Proportional discount: (DiscountAmount / SoldQuantity) * ReturnQuantity
            return DiscountAmountPerUnit * ReturnQuantity;
        }
    }

    /// <summary>
    /// Tax amount per unit.
    /// </summary>
    public decimal TaxPerUnit => PricePerUnit * (TaxRate / 100m);

    /// <summary>
    /// Price per unit including tax.
    /// </summary>
    public decimal PricePerUnitWithTax => PricePerUnit + TaxPerUnit;

    /// <summary>
    /// Calculated refund amount WITHOUT tax: ReturnQuantity * pricePerUnit.
    /// </summary>
    public decimal CalculatedRefundBeforeTax
    {
        get
        {
            if (ReturnQuantity <= 0) return 0;
            return ReturnQuantity * PricePerUnit;
        }
    }

    /// <summary>
    /// Calculated tax amount for the return.
    /// </summary>
    public decimal CalculatedTaxAmount
    {
        get
        {
            if (ReturnQuantity <= 0) return 0;
            return CalculatedRefundBeforeTax * (TaxRate / 100m);
        }
    }

    /// <summary>
    /// Calculated refund amount INCLUDING tax: ReturnQuantity * pricePerUnit * (1 + TaxRate/100).
    /// This is the total amount the customer should receive back.
    /// </summary>
    public decimal CalculatedRefundAmount
    {
        get
        {
            if (ReturnQuantity <= 0) return 0;
            return CalculatedRefundBeforeTax + CalculatedTaxAmount;
        }
    }

    /// <summary>
    /// Whether this item can be returned (has available quantity).
    /// </summary>
    public bool CanReturn => AvailableToReturn > 0;
}
