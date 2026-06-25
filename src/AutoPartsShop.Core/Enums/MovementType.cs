namespace AutoPartsShop.Core.Enums;

/// <summary>
/// Represents the type of stock movement.
/// </summary>
public enum MovementType
{
    /// <summary>Stock added to inventory (purchase, return from customer).</summary>
    In = 0,

    /// <summary>Stock removed from inventory (sale).</summary>
    Out = 1,

    /// <summary>Stock returned by customer.</summary>
    Return = 2,

    /// <summary>Manual stock adjustment (correction).</summary>
    Adjustment = 3
}
