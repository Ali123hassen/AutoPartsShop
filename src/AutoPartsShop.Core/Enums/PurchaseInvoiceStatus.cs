namespace AutoPartsShop.Core.Enums;

/// <summary>
/// Represents the status of a purchase invoice.
/// </summary>
public enum PurchaseInvoiceStatus
{
    /// <summary>Purchase invoice is completed and stock has been added.</summary>
    Completed = 0,

    /// <summary>Purchase invoice has been cancelled and stock reversed.</summary>
    Cancelled = 1
}
