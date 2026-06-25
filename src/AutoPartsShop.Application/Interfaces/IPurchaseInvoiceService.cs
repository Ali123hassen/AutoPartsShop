using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.PurchaseInvoices;

namespace AutoPartsShop.Application.Interfaces;

public interface IPurchaseInvoiceService
{
    Task<PurchaseInvoiceDto> CreatePurchaseInvoiceAsync(CreatePurchaseInvoiceDto dto);
    Task CancelPurchaseInvoiceAsync(int purchaseInvoiceId);
    Task<PurchaseInvoiceDto?> GetByIdAsync(int id);
    Task<PaginatedResult<PurchaseInvoiceDto>> GetPagedAsync(int pageNumber, int pageSize, DateTime? fromDate = null, DateTime? toDate = null);
}
