namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Abstract base class for all domain entities.
/// Provides common identity and auditing fields.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this entity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
