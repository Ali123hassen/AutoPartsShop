using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.Returns;

namespace AutoPartsShop.Application.Interfaces;

public interface IReturnService
{
    Task<ReturnDto> CreateReturnAsync(CreateReturnDto dto);
    Task<List<ReturnDto>> CreateBatchReturnAsync(CreateBatchReturnDto dto);
    Task<List<InvoiceReturnItemDto>> GetInvoiceReturnItemsAsync(int invoiceId);
    Task<ReturnDto?> GetByIdAsync(int id);
    Task<ReturnDetailDto?> GetReturnDetailAsync(int id);
    Task<PaginatedResult<ReturnDto>> GetPagedAsync(int pageNumber, int pageSize, DateTime? fromDate = null, DateTime? toDate = null);
}
