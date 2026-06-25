namespace AutoPartsShop.Core.Enums;

/// <summary>
/// Represents the status of the application license.
/// </summary>
public enum LicenseStatus
{
    /// <summary>No license found - first run or license deleted.</summary>
    NoLicense = 0,

    /// <summary>Trial period is active.</summary>
    TrialActive = 1,

    /// <summary>Trial period has expired.</summary>
    TrialExpired = 2,

    /// <summary>Valid license is active.</summary>
    Active = 3,

    /// <summary>License has expired - needs renewal.</summary>
    Expired = 4,

    /// <summary>Hardware fingerprint mismatch - license tied to different machine.</summary>
    HardwareMismatch = 5,

    /// <summary>License key is invalid or tampered.</summary>
    Invalid = 6,

    /// <summary>Grace period after expiration (3 days).</summary>
    GracePeriod = 7,

    /// <summary>System clock was rolled back (tampering detected).</summary>
    ClockTampered = 8
}
