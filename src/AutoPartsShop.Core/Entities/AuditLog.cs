namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents an audit log entry that tracks changes to entities in the system.
/// </summary>
public sealed class AuditLog : BaseEntity
{
    /// <summary>
    /// Gets or sets the foreign key to the user who performed the action, if available.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Gets or sets the action performed (e.g., "Create", "Update", "Delete").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of entity that was affected (e.g., "SparePart", "Invoice").
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the affected entity, if applicable.
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// Gets or sets the JSON representation of the entity's values before the change.
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// Gets or sets the JSON representation of the entity's values after the change.
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Gets or sets the IP address from which the action was performed.
    /// </summary>
    public string? IpAddress { get; set; }

    // --- Navigation Properties ---

    /// <summary>
    /// Gets or sets the user who performed the action.
    /// </summary>
    public User? User { get; set; }
}
