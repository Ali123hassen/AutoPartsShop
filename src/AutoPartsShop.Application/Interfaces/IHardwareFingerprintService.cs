namespace AutoPartsShop.Application.Interfaces;

/// <summary>
/// Service for generating a unique hardware fingerprint for the current machine.
/// </summary>
public interface IHardwareFingerprintService
{
    /// <summary>
    /// Gets a unique fingerprint based on hardware components (CPU, Motherboard, BIOS, Disk).
    /// </summary>
    Task<string> GetFingerprintAsync();

    /// <summary>
    /// Gets detailed hardware information for display purposes.
    /// </summary>
    Task<HardwareInfo> GetHardwareInfoAsync();
}

/// <summary>
/// Detailed hardware information for display.
/// </summary>
public class HardwareInfo
{
    public string CpuId { get; set; } = string.Empty;
    public string MotherboardSerial { get; set; } = string.Empty;
    public string BiosSerial { get; set; } = string.Empty;
    public string DiskSerial { get; set; } = string.Empty;
    public string MachineGuid { get; set; } = string.Empty;
    public string CombinedFingerprint { get; set; } = string.Empty;
}
