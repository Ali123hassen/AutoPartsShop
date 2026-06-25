using System.Linq.Expressions;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Specifications;

/// <summary>
/// Specification for querying StockMovement entities with navigation properties.
/// Supports filtering by SparePartId, ordering by CreatedAt descending, and paging.
/// </summary>
public class StockMovementSpecification : ISpecification<StockMovement>
{
    public Expression<Func<StockMovement, bool>>? Criteria { get; private set; }
    public List<Expression<Func<StockMovement, object>>> Includes { get; private set; } = [];
    public List<string> IncludeStrings { get; private set; } = [];
    public Expression<Func<StockMovement, object>>? OrderBy { get; private set; }
    public Expression<Func<StockMovement, object>>? OrderByDescending { get; private set; }
    public int? Take { get; private set; }
    public int? Skip { get; private set; }
    public bool IsPagingEnabled { get; private set; }

    /// <summary>
    /// Creates a specification for all stock movements with navigation properties loaded.
    /// </summary>
    /// <param name="count">Maximum number of records to return.</param>
    public StockMovementSpecification(int count = 100)
    {
        AddInclude(m => m.SparePart);
        AddInclude(m => m.User);
        ApplyOrderByDescending(m => m.CreatedAt);

        if (count > 0)
        {
            Skip = 0;
            Take = count;
            IsPagingEnabled = true;
        }
    }

    /// <summary>
    /// Creates a specification for stock movements of a specific spare part.
    /// </summary>
    /// <param name="sparePartId">The spare part ID to filter by.</param>
    /// <param name="count">Maximum number of records to return.</param>
    public StockMovementSpecification(int sparePartId, int count = 50)
    {
        Criteria = m => m.SparePartId == sparePartId;
        AddInclude(m => m.SparePart);
        AddInclude(m => m.User);
        ApplyOrderByDescending(m => m.CreatedAt);

        if (count > 0)
        {
            Skip = 0;
            Take = count;
            IsPagingEnabled = true;
        }
    }

    private void AddInclude(Expression<Func<StockMovement, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    private void ApplyOrderBy(Expression<Func<StockMovement, object>> orderByExpression)
    {
        OrderBy = orderByExpression;
    }

    private void ApplyOrderByDescending(Expression<Func<StockMovement, object>> orderByDescExpression)
    {
        OrderByDescending = orderByDescExpression;
    }
}
