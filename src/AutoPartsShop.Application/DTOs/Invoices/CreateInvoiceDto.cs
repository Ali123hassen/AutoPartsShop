using AutoPartsShop.Core.Enums;

namespace AutoPartsShop.Application.DTOs.Invoices;

public class CreateInvoiceDto
{
    public List<InvoiceItemDto> Items { get; set; } = [];
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? CustomerName { get; set; }
    public string? Notes { get; set; }
}
