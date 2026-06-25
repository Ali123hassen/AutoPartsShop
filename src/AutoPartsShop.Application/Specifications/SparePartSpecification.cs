using System.Linq.Expressions;
using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Specifications;

/// <summary>
/// Specification for searching and filtering SparePart entities.
/// Supports keyword search (Name, PartNumber, Barcode), category filter,
/// low-stock filter, and paging.
/// </summary>
public class SparePartSpecification : ISpecification<SparePart>
{
    public Expression<Func<SparePart, bool>>? Criteria { get; private set; }
    public List<Expression<Func<SparePart, object>>> Includes { get; private set; } = [];
    public List<string> IncludeStrings { get; private set; } = [];
    public Expression<Func<SparePart, object>>? OrderBy { get; private set; }
    public Expression<Func<SparePart, object>>? OrderByDescending { get; private set; }
    public int? Take { get; private set; }
    public int? Skip { get; private set; }
    public bool IsPagingEnabled { get; private set; }

    public SparePartSpecification(SparePartSearchDto search)
    {
        AddInclude(sp => sp.Category);
        ApplyOrderByDescending(sp => sp.Id);

        var criteria = BuildCriteria(search);
        if (criteria is not null)
            Criteria = criteria;

        if (search.PageNumber > 0 && search.PageSize > 0)
        {
            Skip = (search.PageNumber - 1) * search.PageSize;
            Take = search.PageSize;
            IsPagingEnabled = true;
        }
    }

    /// <summary>
    /// Creates a specification for low-stock spare parts only.
    /// </summary>
    public SparePartSpecification(bool lowStockOnly) : this(new SparePartSearchDto { LowStockOnly = true })
    {
    }

    private static Expression<Func<SparePart, bool>>? BuildCriteria(SparePartSearchDto search)
    {
        Expression<Func<SparePart, bool>>? criteria = null;

        // Keyword search across Name, PartNumber, and Barcode
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var keyword = search.Keyword.Trim().ToLower();
            Expression<Func<SparePart, bool>> keywordCriteria = sp =>
                sp.Name.ToLower().Contains(keyword) ||
                sp.PartNumber.ToLower().Contains(keyword) ||
                sp.Barcode.ToLower().Contains(keyword);
            criteria = criteria is null ? keywordCriteria : CombineAnd(criteria, keywordCriteria);
        }

        // PartNumber filter
        if (!string.IsNullOrWhiteSpace(search.PartNumber))
        {
            var partNumber = search.PartNumber.Trim().ToLower();
            Expression<Func<SparePart, bool>> partNumberCriteria = sp =>
                sp.PartNumber.ToLower().Contains(partNumber);
            criteria = criteria is null ? partNumberCriteria : CombineAnd(criteria, partNumberCriteria);
        }

        // Barcode filter
        if (!string.IsNullOrWhiteSpace(search.Barcode))
        {
            var barcode = search.Barcode.Trim().ToLower();
            Expression<Func<SparePart, bool>> barcodeCriteria = sp =>
                sp.Barcode.ToLower().Contains(barcode);
            criteria = criteria is null ? barcodeCriteria : CombineAnd(criteria, barcodeCriteria);
        }

        // Location filter
        if (!string.IsNullOrWhiteSpace(search.Location))
        {
            var location = search.Location.Trim().ToLower();
            Expression<Func<SparePart, bool>> locationCriteria = sp =>
                sp.Location != null && sp.Location.ToLower().Contains(location);
            criteria = criteria is null ? locationCriteria : CombineAnd(criteria, locationCriteria);
        }

        // Category filter
        if (search.CategoryId.HasValue)
        {
            var categoryId = search.CategoryId.Value;
            Expression<Func<SparePart, bool>> categoryCriteria = sp =>
                sp.CategoryId == categoryId;
            criteria = criteria is null ? categoryCriteria : CombineAnd(criteria, categoryCriteria);
        }

        // Low stock only filter
        if (search.LowStockOnly == true)
        {
            Expression<Func<SparePart, bool>> lowStockCriteria = sp =>
                sp.CurrentStock <= sp.MinStockLevel;
            criteria = criteria is null ? lowStockCriteria : CombineAnd(criteria, lowStockCriteria);
        }

        // Active only filter (default: show only active items)
        if (search.IsActive == true)
        {
            Expression<Func<SparePart, bool>> activeCriteria = sp =>
                sp.IsActive == true;
            criteria = criteria is null ? activeCriteria : CombineAnd(criteria, activeCriteria);
        }

        return criteria;
    }

    private static Expression<Func<SparePart, bool>> CombineAnd(
        Expression<Func<SparePart, bool>> first,
        Expression<Func<SparePart, bool>> second)
    {
        var parameter = Expression.Parameter(typeof(SparePart), "sp");

        var leftVisitor = new ReplaceExpressionVisitor(first.Parameters[0], parameter);
        var left = leftVisitor.Visit(first.Body);

        var rightVisitor = new ReplaceExpressionVisitor(second.Parameters[0], parameter);
        var right = rightVisitor.Visit(second.Body);

        return Expression.Lambda<Func<SparePart, bool>>(
            Expression.AndAlso(left!, right!), parameter);
    }

    private void AddInclude(Expression<Func<SparePart, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    private void ApplyOrderBy(Expression<Func<SparePart, object>> orderByExpression)
    {
        OrderBy = orderByExpression;
    }

    private void ApplyOrderByDescending(Expression<Func<SparePart, object>> orderByDescExpression)
    {
        OrderByDescending = orderByDescExpression;
    }

    /// <summary>
    /// Helper visitor to replace parameter expressions when combining lambda expressions.
    /// </summary>
    private class ReplaceExpressionVisitor : ExpressionVisitor
    {
        private readonly Expression _oldValue;
        private readonly Expression _newValue;

        public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
        {
            _oldValue = oldValue;
            _newValue = newValue;
        }

        public override Expression? Visit(Expression? node)
        {
            return node == _oldValue ? _newValue : base.Visit(node);
        }
    }
}
