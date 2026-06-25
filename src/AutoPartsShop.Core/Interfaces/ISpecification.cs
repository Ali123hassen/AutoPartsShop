using System.Linq.Expressions;

namespace AutoPartsShop.Core.Interfaces;

/// <summary>
/// Defines a specification pattern for building complex query criteria
/// in a reusable and composable manner.
/// </summary>
/// <typeparam name="T">The entity type this specification applies to.</typeparam>
public interface ISpecification<T>
{
    /// <summary>
    /// Gets the filter criteria (WHERE clause) as a predicate expression.
    /// </summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>
    /// Gets the list of include expressions for eager-loading navigation properties.
    /// </summary>
    List<Expression<Func<T, object>>> Includes { get; }

    /// <summary>
    /// Gets the list of string-based include paths for eager-loading nested navigation properties (e.g. "Items.SparePart").
    /// </summary>
    List<string> IncludeStrings { get; }

    /// <summary>
    /// Gets the ordering expression for ascending sort.
    /// </summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>
    /// Gets the ordering expression for descending sort.
    /// </summary>
    Expression<Func<T, object>>? OrderByDescending { get; }

    /// <summary>
    /// Gets the number of records to take (for paging).
    /// </summary>
    int? Take { get; }

    /// <summary>
    /// Gets the number of records to skip (for paging).
    /// </summary>
    int? Skip { get; }

    /// <summary>
    /// Gets whether paging is enabled for this specification.
    /// </summary>
    bool IsPagingEnabled { get; }
}
