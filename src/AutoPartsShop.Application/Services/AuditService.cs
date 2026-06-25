using AutoMapper;
using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.Auth;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Services;

public class AuditService : IAuditService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUserService;

    public AuditService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUserService = currentUserService;
    }

    public async Task LogAsync(string action, string entityType, int? entityId = null, string? oldValues = null, string? newValues = null)
    {
        var auditLog = new AuditLog
        {
            UserId = _currentUserService.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
        };

        await _unitOfWork.AuditLogs.AddAsync(auditLog);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task LogErrorAsync(string action, string errorMessage)
    {
        var auditLog = new AuditLog
        {
            UserId = _currentUserService.UserId,
            Action = action,
            EntityType = "Error",
            EntityId = null,
            OldValues = null,
            NewValues = errorMessage,
        };

        await _unitOfWork.AuditLogs.AddAsync(auditLog);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<PaginatedResult<AuditLogDto>> GetLogsAsync(int pageNumber, int pageSize, string? entityType = null, DateTime? fromDate = null)
    {
        var allLogs = await _unitOfWork.AuditLogs.GetAllAsync();

        var filtered = allLogs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(entityType))
            filtered = filtered.Where(l => l.EntityType == entityType);

        if (fromDate.HasValue)
            filtered = filtered.Where(l => l.CreatedAt >= fromDate.Value);

        var totalCount = filtered.Count();
        var paged = filtered
            .OrderByDescending(l => l.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = paged.Select(_mapper.Map<AuditLogDto>).ToList();

        return new PaginatedResult<AuditLogDto>(dtos, totalCount, pageNumber, pageSize);
    }
}
