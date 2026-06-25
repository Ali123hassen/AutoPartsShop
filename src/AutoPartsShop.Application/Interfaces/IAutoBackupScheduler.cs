namespace AutoPartsShop.Application.Interfaces;

/// <summary>
/// Manages automatic database backup scheduling with a background timer.
/// Reads settings from the database and creates backups at the configured interval.
/// </summary>
public interface IAutoBackupScheduler
{
    /// <summary>
    /// Starts the auto backup scheduler.
    /// Reads settings from the database and starts the timer if auto backup is enabled.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Starts the auto backup scheduler with explicit settings.
    /// Use this when the caller already has the settings and wants to bypass the database read.
    /// </summary>
    /// <param name="intervalMinutes">Backup interval in minutes (minimum 5)</param>
    /// <param name="backupDirectory">Directory where backups will be stored</param>
    Task StartAsync(int intervalMinutes, string backupDirectory);

    /// <summary>
    /// Stops the auto backup scheduler and disposes the timer.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Restarts the scheduler with updated settings.
    /// Call this when backup settings are changed.
    /// </summary>
    Task RestartAsync();

    /// <summary>
    /// Gets whether the scheduler is currently active and running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the last time a backup was automatically created.
    /// </summary>
    DateTime? LastBackupTime { get; }

    /// <summary>
    /// Event raised when an auto backup is completed (successfully or not).
    /// </summary>
    event EventHandler<AutoBackupResult>? BackupCompleted;
}

/// <summary>
/// Result of an automatic backup operation.
/// </summary>
public class AutoBackupResult
{
    public bool IsSuccess { get; set; }
    public string? BackupPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
