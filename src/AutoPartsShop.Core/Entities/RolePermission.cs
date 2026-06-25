namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a granular permission key assigned to a role.
/// </summary>
public sealed class RolePermission : BaseEntity
{
    /// <summary>
    /// Gets or sets the foreign key to the owning role.
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// Gets or sets the permission key (e.g., "Sales.Create", "Reports.View").
    /// </summary>
    public string PermissionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this permission is granted.
    /// </summary>
    public bool CanAccess { get; set; } = true;

    // --- Navigation Properties ---

    /// <summary>
    /// Gets or sets the role that owns this permission.
    /// </summary>
    public Role Role { get; set; } = null!;
}
