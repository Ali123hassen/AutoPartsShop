using AutoPartsShop.Application.DTOs;
using AutoPartsShop.Core.Enums;

namespace AutoPartsShop.Application.Interfaces;

/// <summary>
/// Service for managing application licensing.
/// </summary>
public interface ILicenseService
{
    /// <summary>
    /// Validates the current license and returns the result.
    /// </summary>
    Task<LicenseValidationResult> ValidateLicenseAsync();

    /// <summary>
    /// Activates a license with the given key.
    /// </summary>
    Task<LicenseValidationResult> ActivateLicenseAsync(string licenseKey);

    /// <summary>
    /// Gets the current license info if available.
    /// </summary>
    Task<LicenseInfo?> GetCurrentLicenseAsync();

    /// <summary>
    /// Gets the hardware fingerprint of the current machine.
    /// </summary>
    Task<string> GetHardwareFingerprintAsync();

    /// <summary>
    /// Deactivates and removes the current license.
    /// </summary>
    Task DeactivateLicenseAsync();

    /// <summary>
    /// Checks if periodic verification is needed and performs it.
    /// Returns true if verification was performed.
    /// </summary>
    Task<bool> CheckPeriodicVerificationAsync();

    /// <summary>
    /// Starts the trial period if no license or trial exists.
    /// </summary>
    Task<LicenseValidationResult> StartTrialAsync();

    /// <summary>
    /// Generates a license key for a given hardware fingerprint and expiration date.
    /// This is used by the admin License Generator tool.
    /// </summary>
    string GenerateLicenseKey(string hardwareFingerprint, DateTime expirationDate, string customerName, LicenseType licenseType, int maxUsers = 1);
}
