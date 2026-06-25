using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.Reports;
using AutoPartsShop.Application.DTOs.Returns;

namespace AutoPartsShop.Application.Interfaces;

public interface IReportService
{
    Task<DailySalesReportDto> GetDailySalesReportAsync(DateTime startDate, DateTime endDate);
    Task<ProfitReportDto> GetProfitReportAsync(DateTime startDate, DateTime endDate);
    Task<StockReportDto> GetStockReportAsync();
    Task<List<TopSellingPartDto>> GetTopSellingPartsAsync(DateTime startDate, DateTime endDate, int top = 20);
    Task<PaginatedResult<ReturnDto>> GetReturnsReportAsync(DateTime startDate, DateTime endDate, int pageNumber = 1, int pageSize = 50);
    Task<byte[]> GenerateReportPdfAsync(string reportType, object data);

    /// <summary>
    /// يجلب بيانات المبيعات اليومية لنطاق تاريخ في استدعاء واحد.
    /// أكثر كفاءة من استدعاء GetDailySalesReportAsync لكل يوم على حدة.
    /// </summary>
    Task<Dictionary<DateTime, (decimal NetSales, decimal Profit)>> GetDailySalesChartDataAsync(DateTime startDate, DateTime endDate);
}
