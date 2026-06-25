namespace AutoPartsShop.Application.DTOs.Returns;

/// <summary>
/// Detailed DTO for a return, including financial breakdown and invoice info.
/// </summary>
public class ReturnDetailDto
{
    // === Basic Return Info ===
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public string ReturnType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal RefundAmount { get; set; }
    public string? Reason { get; set; }

    // === Part Info ===
    public string PartName { get; set; } = string.Empty;
    public string? PartNumber { get; set; }

    // === Replacement Part (for Exchange) ===
    public string? ReplacementPartName { get; set; }

    // === Invoice Info ===
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public string? CustomerName { get; set; }
    public string? InvoiceUserName { get; set; }

    // === Financial Breakdown (from invoice item) ===
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PriceAfterDiscount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalRefundAmount { get; set; }

    /// <summary>
    /// Subtotal before tax: PriceAfterDiscount * Quantity
    /// </summary>
    public decimal SubtotalBeforeTax { get; set; }

    // === User Info ===
    public string UserName { get; set; } = string.Empty;
}
