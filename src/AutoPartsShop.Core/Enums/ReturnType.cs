namespace AutoPartsShop.Core.Enums;

/// <summary>
/// Represents the type of return transaction.
/// </summary>
public enum ReturnType
{
    /// <summary>Customer receives a monetary refund.</summary>
    Refund = 0,

    /// <summary>Customer exchanges the item for a replacement.</summary>
    Exchange = 1
}
