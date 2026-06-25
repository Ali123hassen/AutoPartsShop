using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.Auth;

namespace AutoPartsShop.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, int? entityId = null, string? oldValues = null, string? newValues = null);
    Task LogErrorAsync(string action, string errorMessage);
    Task<PaginatedResult<AuditLogDto>> GetLogsAsync(int pageNumber, int pageSize, string? entityType = null, DateTime? fromDate = null);
}
