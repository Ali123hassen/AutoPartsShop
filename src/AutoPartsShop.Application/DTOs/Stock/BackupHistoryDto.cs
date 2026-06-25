namespace AutoPartsShop.Application.DTOs.Stock;

public class BackupHistoryDto
{
    public int Id { get; set; }
    public DateTime BackupDate { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public decimal? FileSizeMB { get; set; }
    public string BackupType { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public string? Notes { get; set; }
}
