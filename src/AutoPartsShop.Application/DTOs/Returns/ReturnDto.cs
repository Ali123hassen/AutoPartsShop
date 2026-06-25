namespace AutoPartsShop.Application.DTOs.Returns;

public class ReturnDto
{
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public DateTime ReturnDate { get; set; }
    public string ReturnType { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string? ReplacementPartName { get; set; }
    public int Quantity { get; set; }
    public decimal RefundAmount { get; set; }
    public string? Reason { get; set; }
    public string UserName { get; set; } = string.Empty;
}
