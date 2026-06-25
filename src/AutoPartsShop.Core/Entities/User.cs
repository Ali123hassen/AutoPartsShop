namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a system user who can authenticate and perform operations.
/// </summary>
public sealed class User : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique username for login.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hashed password.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full display name of the user.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the foreign key to the user's assigned role.
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// Gets or sets whether this user account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the last login timestamp, if any.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    // --- Navigation Properties ---

    /// <summary>
    /// Gets or sets the role assigned to this user.
    /// </summary>
    public Role Role { get; set; } = null!;

    /// <summary>
    /// Gets the collection of audit log entries created by this user.
    /// </summary>
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
