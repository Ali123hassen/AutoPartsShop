using AutoPartsShop.Core.Enums;

namespace AutoPartsShop.Application.DTOs.Returns;

/// <summary>
/// DTO for creating a batch return — multiple items returned from a single invoice.
/// </summary>
public class CreateBatchReturnDto
{
    /// <summary>The invoice ID these returns belong to.</summary>
    public int InvoiceId { get; set; }

    /// <summary>The type of return (Refund or Exchange).</summary>
    public ReturnType ReturnType { get; set; } = ReturnType.Refund;

    /// <summary>Optional replacement part ID (for exchange type).</summary>
    public int? ReplacementPartId { get; set; }

    /// <summary>Common reason for all items in this batch.</summary>
    public string? Reason { get; set; }

    /// <summary>List of individual items to return.</summary>
    public List<BatchReturnItemDto> Items { get; set; } = [];
}

/// <summary>
/// Represents a single item within a batch return.
/// </summary>
public class BatchReturnItemDto
{
    /// <summary>Foreign key to the spare part being returned.</summary>
    public int SparePartId { get; set; }

    /// <summary>Quantity being returned for this item.</summary>
    public int Quantity { get; set; }

    /// <summary>Refund amount for this item.</summary>
    public decimal RefundAmount { get; set; }
}
