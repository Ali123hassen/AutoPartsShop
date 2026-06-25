using AutoPartsShop.Application.DTOs.Stock;

namespace AutoPartsShop.Application.Interfaces;

public interface IBackupService
{
    Task<string> CreateBackupAsync(string backupDirectory);
    Task RestoreBackupAsync(string backupFilePath);
    Task<List<BackupHistoryDto>> GetBackupHistoryAsync();
    Task ScheduleAutoBackupAsync(string backupDirectory, int intervalHours);
}
