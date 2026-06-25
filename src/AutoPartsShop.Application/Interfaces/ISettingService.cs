namespace AutoPartsShop.Application.Interfaces;

/// <summary>
/// Service for managing application settings stored as key-value pairs.
/// </summary>
public interface ISettingService
{
    /// <summary>
    /// Gets a setting value by key. Returns the default value if not found.
    /// </summary>
    Task<string> GetAsync(string key, string defaultValue = "");

    /// <summary>
    /// Sets a setting value by key. Creates the setting if it doesn't exist.
    /// </summary>
    Task SetAsync(string key, string value, string? description = null);

    /// <summary>
    /// Gets all settings as a dictionary.
    /// </summary>
    Task<Dictionary<string, string>> GetAllAsync();

    /// <summary>
    /// Saves multiple settings at once.
    /// </summary>
    Task SaveSettingsAsync(Dictionary<string, string> settings);
}
