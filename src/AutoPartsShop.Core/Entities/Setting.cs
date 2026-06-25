namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents an application setting stored as a key-value pair.
/// </summary>
public sealed class Setting : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique setting key.
    /// </summary>
    public string SettingKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the setting value.
    /// </summary>
    public string SettingValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description of this setting.
    /// </summary>
    public string? Description { get; set; }
}
