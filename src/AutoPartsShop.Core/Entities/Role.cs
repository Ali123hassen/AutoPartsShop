namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a security role that groups permissions for users.
/// </summary>
public sealed class Role : BaseEntity
{
    /// <summary>
    /// Gets or sets the role name (e.g., "Admin", "Cashier").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description of the role.
    /// </summary>
    public string? Description { get; set; }

    // --- Navigation Properties ---

    /// <summary>
    /// Gets the collection of users assigned to this role.
    /// </summary>
    public ICollection<User> Users { get; set; } = new List<User>();

    /// <summary>
    /// Gets the collection of permissions defined for this role.
    /// </summary>
    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}
