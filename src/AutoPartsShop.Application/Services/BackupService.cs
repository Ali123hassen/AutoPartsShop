using AutoMapper;
using AutoPartsShop.Application.DTOs.Stock;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Services;

public class BackupService : IBackupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;
    private readonly IDatabaseBackupExecutor _backupExecutor;

    public BackupService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IAuditService auditService,
        IDatabaseBackupExecutor backupExecutor)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _auditService = auditService;
        _backupExecutor = backupExecutor;
    }

    public async Task<string> CreateBackupAsync(string backupDirectory)
    {
        var fileName = $"AutoPartsShop_Backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
        var filePath = Path.Combine(backupDirectory, fileName);

        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(backupDirectory);

            // Execute the actual database backup
            await _backupExecutor.ExecuteBackupAsync(filePath);

            // Get file size
            decimal fileSizeMB = 0;
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                fileSizeMB = Math.Round((decimal)fileInfo.Length / (1024 * 1024), 2);
            }

            // Record successful backup
            var backupHistory = new BackupHistory
            {
                BackupDate = DateTime.UtcNow,
                FilePath = filePath,
                FileSizeMB = fileSizeMB,
                BackupType = 0, // Full
                IsSuccessful = true,
                Notes = $"Full backup created successfully. Size: {fileSizeMB} MB"
            };

            await _unitOfWork.BackupHistories.AddAsync(backupHistory);
            await _unitOfWork.SaveChangesAsync();

            await _auditService.LogAsync("CreateBackup", "BackupHistory", backupHistory.Id,
                null, $"Backup created: {filePath} ({fileSizeMB} MB)");

            return filePath;
        }
        catch (Exception ex)
        {
            // Record failed backup
            try
            {
                var failedBackup = new BackupHistory
                {
                    BackupDate = DateTime.UtcNow,
                    FilePath = filePath,
                    FileSizeMB = null,
                    BackupType = 0,
                    IsSuccessful = false,
                    Notes = $"Backup failed: {ex.Message}"
                };

                await _unitOfWork.BackupHistories.AddAsync(failedBackup);
                await _unitOfWork.SaveChangesAsync();
            }
            catch
            {
                // If we can't even log the failure, just continue
            }

            try
            {
                await _auditService.LogErrorAsync("BackupFailed", ex.Message);
            }
            catch
            {
                // Ignore audit logging failures
            }

            throw new InvalidOperationException($"فشل إنشاء النسخة الاحتياطية: {ex.Message}", ex);
        }
    }

    public async Task RestoreBackupAsync(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException($"ملف النسخة الاحتياطية غير موجود: {backupFilePath}");

        try
        {
            // Execute the actual database restore
            await _backupExecutor.ExecuteRestoreAsync(backupFilePath);

            await _auditService.LogAsync("RestoreBackup", "BackupHistory", null,
                null, $"Database restored from: {backupFilePath}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"فشل استعادة النسخة الاحتياطية: {ex.Message}", ex);
        }
    }

    public async Task<List<BackupHistoryDto>> GetBackupHistoryAsync()
    {
        var allBackups = await _unitOfWork.BackupHistories.GetAllAsync();
        var sortedBackups = allBackups
            .OrderByDescending(b => b.BackupDate)
            .ToList();

        return sortedBackups.Select(_mapper.Map<BackupHistoryDto>).ToList();
    }

    public async Task ScheduleAutoBackupAsync(string backupDirectory, int intervalHours)
    {
        await _auditService.LogAsync("ScheduleBackup", "Setting", null,
            null, $"Auto backup scheduled every {intervalHours} hours to: {backupDirectory}");
    }
}
