using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.Invoices;

namespace AutoPartsShop.Application.Interfaces;

public interface IInvoiceService
{
    Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceDto dto);
    Task CancelInvoiceAsync(int invoiceId);
    Task<InvoiceDto?> GetByIdAsync(int id);
    Task<InvoiceDto?> GetByNumberAsync(string invoiceNumber);
    Task<PaginatedResult<InvoiceDto>> GetPagedAsync(int pageNumber, int pageSize, DateTime? fromDate = null, DateTime? toDate = null);
    Task<decimal> GetDailySalesTotalAsync(DateTime date);
}
