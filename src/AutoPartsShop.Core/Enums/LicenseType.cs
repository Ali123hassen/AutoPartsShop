namespace AutoPartsShop.Core.Enums;

/// <summary>
/// Represents the type of license.
/// </summary>
public enum LicenseType
{
    /// <summary>Trial license with limited duration.</summary>
    Trial = 0,

    /// <summary>Standard license - single station.</summary>
    Standard = 1,

    /// <summary>Professional license - multi-station support.</summary>
    Professional = 2,

    /// <summary>Enterprise license - unlimited stations.</summary>
    Enterprise = 3
}
