using AutoPartsShop.Core.Enums;

namespace AutoPartsShop.Application.DTOs;

/// <summary>
/// Result of a license validation check.
/// </summary>
public class LicenseValidationResult
{
    public bool IsValid { get; set; }
    public LicenseStatus Status { get; set; } = LicenseStatus.NoLicense;
    public string Message { get; set; } = string.Empty;
    public string MessageAr { get; set; } = string.Empty;
    public LicenseInfo? License { get; set; }
    public int DaysRemaining { get; set; }
    public bool ShouldShowActivation { get; set; }

    public static LicenseValidationResult Valid(LicenseInfo license)
    {
        return new LicenseValidationResult
        {
            IsValid = true,
            Status = license.IsTrial ? LicenseStatus.TrialActive : LicenseStatus.Active,
            License = license,
            DaysRemaining = license.DaysRemaining,
            ShouldShowActivation = false
        };
    }

    public static LicenseValidationResult Expired(int daysOverdue)
    {
        return new LicenseValidationResult
        {
            IsValid = false,
            Status = LicenseStatus.Expired,
            Message = $"License expired {daysOverdue} day(s) ago.",
            MessageAr = $"انتهت صلاحية الترخيص منذ {daysOverdue} يوم.",
            DaysRemaining = 0,
            ShouldShowActivation = true
        };
    }

    public static LicenseValidationResult TrialExpired()
    {
        return new LicenseValidationResult
        {
            IsValid = false,
            Status = LicenseStatus.TrialExpired,
            Message = "Trial period has expired.",
            MessageAr = "انتهت فترة التجربة.",
            DaysRemaining = 0,
            ShouldShowActivation = true
        };
    }

    public static LicenseValidationResult HardwareMismatch()
    {
        return new LicenseValidationResult
        {
            IsValid = false,
            Status = LicenseStatus.HardwareMismatch,
            Message = "License is tied to a different machine.",
            MessageAr = "الترخيص مرتبط بجهاز مختلف.",
            DaysRemaining = 0,
            ShouldShowActivation = true
        };
    }

    public static LicenseValidationResult Invalid(string reason)
    {
        return new LicenseValidationResult
        {
            IsValid = false,
            Status = LicenseStatus.Invalid,
            Message = $"Invalid license: {reason}",
            MessageAr = $"ترخيص غير صالح: {reason}",
            DaysRemaining = 0,
            ShouldShowActivation = true
        };
    }

    public static LicenseValidationResult NoLicense()
    {
        return new LicenseValidationResult
        {
            IsValid = false,
            Status = LicenseStatus.NoLicense,
            Message = "No license found.",
            MessageAr = "لم يتم العثور على ترخيص.",
            DaysRemaining = 0,
            ShouldShowActivation = true
        };
    }

    public static LicenseValidationResult GracePeriod(int daysRemaining)
    {
        return new LicenseValidationResult
        {
            IsValid = false,
            Status = LicenseStatus.GracePeriod,
            Message = $"License expired. Grace period: {daysRemaining} day(s) remaining.",
            MessageAr = $"انتهت صلاحية الترخيص. فترة السماح: متبقي {daysRemaining} يوم.",
            DaysRemaining = daysRemaining,
            ShouldShowActivation = false
        };
    }

    public static LicenseValidationResult ClockRollbackDetected()
    {
        return new LicenseValidationResult
        {
            IsValid = false,
            Status = LicenseStatus.ClockTampered,
            Message = "System clock has been rolled back. Please restore the correct date.",
            MessageAr = "تم رجوع تاريخ النظام. يرجى استعادة التاريخ الصحيح.",
            DaysRemaining = 0,
            ShouldShowActivation = true
        };
    }
}
