namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a database backup history record.
/// </summary>
public sealed class BackupHistory : BaseEntity
{
    /// <summary>
    /// Gets or sets the date and time when the backup was performed.
    /// </summary>
    public DateTime BackupDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the file path where the backup is stored.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the backup file in megabytes.
    /// </summary>
    public decimal? FileSizeMB { get; set; }

    /// <summary>
    /// Gets or sets the type of backup (e.g., 0 = Full, 1 = Incremental).
    /// </summary>
    public int BackupType { get; set; }

    /// <summary>
    /// Gets or sets whether the backup completed successfully.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the user who initiated the backup, if applicable.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Gets or sets optional notes about the backup.
    /// </summary>
    public string? Notes { get; set; }
}
