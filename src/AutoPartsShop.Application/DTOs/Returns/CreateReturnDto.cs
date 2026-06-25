using AutoPartsShop.Core.Enums;

namespace AutoPartsShop.Application.DTOs.Returns;

public class CreateReturnDto
{
    public int? InvoiceId { get; set; }
    public int SparePartId { get; set; }
    public int? ReplacementPartId { get; set; }
    public ReturnType ReturnType { get; set; }
    public int Quantity { get; set; }
    public decimal RefundAmount { get; set; }
    public string? Reason { get; set; }
}
