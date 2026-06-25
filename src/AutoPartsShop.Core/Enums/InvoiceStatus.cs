namespace AutoPartsShop.Core.Enums;

/// <summary>
/// Represents the status of an invoice.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Invoice is completed and finalized with no returns.</summary>
    Completed = 0,

    /// <summary>Invoice has been cancelled.</summary>
    Cancelled = 1,

    /// <summary>Invoice has some items returned (partial return).</summary>
    PartialReturn = 2,

    /// <summary>All items in the invoice have been returned (full return).</summary>
    FullReturn = 3
}
