namespace AutoPartsShop.Core.Entities;

/// <summary>
/// Represents a product category, supporting hierarchical (parent-child) structure.
/// </summary>
public sealed class Category : BaseEntity
{
    /// <summary>
    /// Gets or sets the category name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the foreign key to the parent category, if this is a sub-category.
    /// </summary>
    public int? ParentCategoryId { get; set; }

    /// <summary>
    /// Gets or sets an optional description of the category.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this category is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // --- Navigation Properties ---

    /// <summary>
    /// Gets or sets the parent category, if any.
    /// </summary>
    public Category? ParentCategory { get; set; }

    /// <summary>
    /// Gets the collection of child (sub) categories.
    /// </summary>
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();

    /// <summary>
    /// Gets the collection of spare parts belonging to this category.
    /// </summary>
    public ICollection<SparePart> SpareParts { get; set; } = new List<SparePart>();
}
