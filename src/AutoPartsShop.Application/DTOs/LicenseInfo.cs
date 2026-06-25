using AutoPartsShop.Core.Enums;

namespace AutoPartsShop.Application.DTOs;

/// <summary>
/// Contains all license information after validation.
/// </summary>
public class LicenseInfo
{
    public LicenseStatus Status { get; set; } = LicenseStatus.NoLicense;
    public LicenseType LicenseType { get; set; } = LicenseType.Trial;
    public string CustomerName { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public string HardwareFingerprint { get; set; } = string.Empty;
    public DateTime ActivationDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public DateTime LastVerificationDate { get; set; }
    public bool IsTrial { get; set; } = true;
    public DateTime? TrialStartDate { get; set; }
    public int DaysRemaining { get; set; }
    public int MaxUsers { get; set; } = 1;

    /// <summary>
    /// آخر تاريخ مسجل للنظام - يُستخدم لكشف التلاعب بالساعة.
    /// إذا كان التاريخ الحالي أقدم من هذا التاريخ، فهذا يعني أن المستخدم رجع الساعة.
    /// </summary>
    public DateTime? LastKnownDate { get; set; }
}
