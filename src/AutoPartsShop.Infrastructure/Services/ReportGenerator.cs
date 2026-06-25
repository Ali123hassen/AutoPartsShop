using AutoPartsShop.Application.Interfaces;

namespace AutoPartsShop.Infrastructure.Services;

public class ReportGenerator : IReportService
{
    public Task<Application.DTOs.Reports.DailySalesReportDto> GetDailySalesReportAsync(DateTime startDate, DateTime endDate)
    {
        throw new NotImplementedException("FastReport integration is not yet implemented. Please install FastReport package and configure the report engine.");
    }

    public Task<Application.DTOs.Reports.ProfitReportDto> GetProfitReportAsync(DateTime startDate, DateTime endDate)
    {
        throw new NotImplementedException("FastReport integration is not yet implemented. Please install FastReport package and configure the report engine.");
    }

    public Task<Application.DTOs.Reports.StockReportDto> GetStockReportAsync()
    {
        throw new NotImplementedException("FastReport integration is not yet implemented. Please install FastReport package and configure the report engine.");
    }

    public Task<List<Application.DTOs.Reports.TopSellingPartDto>> GetTopSellingPartsAsync(DateTime startDate, DateTime endDate, int top = 20)
    {
        throw new NotImplementedException("FastReport integration is not yet implemented. Please install FastReport package and configure the report engine.");
    }

    public Task<Application.Common.PaginatedResult<Application.DTOs.Returns.ReturnDto>> GetReturnsReportAsync(DateTime startDate, DateTime endDate, int pageNumber = 1, int pageSize = 50)
    {
        throw new NotImplementedException("FastReport integration is not yet implemented. Please install FastReport package and configure the report engine.");
    }

    public Task<byte[]> GenerateReportPdfAsync(string reportType, object data)
    {
        throw new NotImplementedException("FastReport integration is not yet implemented. Please install FastReport package and configure the report engine.");
    }

    public Task<Dictionary<DateTime, (decimal NetSales, decimal Profit)>> GetDailySalesChartDataAsync(DateTime startDate, DateTime endDate)
    {
        throw new NotImplementedException("FastReport integration is not yet implemented. Please install FastReport package and configure the report engine.");
    }
}
