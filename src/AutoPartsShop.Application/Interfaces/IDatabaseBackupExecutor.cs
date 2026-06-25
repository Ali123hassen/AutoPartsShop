namespace AutoPartsShop.Application.Interfaces;

/// <summary>
/// Abstraction for executing database backup/restore operations.
/// Implemented in the Infrastructure layer with actual SQL Server commands.
/// </summary>
public interface IDatabaseBackupExecutor
{
    /// <summary>
    /// Creates a full backup of the database to the specified file path.
    /// </summary>
    /// <param name="backupFilePath">The full path where the backup file will be created.</param>
    Task ExecuteBackupAsync(string backupFilePath);

    /// <summary>
    /// Restores the database from the specified backup file.
    /// </summary>
    /// <param name="backupFilePath">The full path of the backup file to restore from.</param>
    Task ExecuteRestoreAsync(string backupFilePath);
}
