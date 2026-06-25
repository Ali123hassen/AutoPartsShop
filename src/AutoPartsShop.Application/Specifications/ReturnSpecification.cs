using System.Linq.Expressions;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Specifications;

/// <summary>
/// Specification for filtering Return entities.
/// </summary>
public class ReturnSpecification : ISpecification<Return>
{
    public Expression<Func<Return, bool>>? Criteria { get; private set; }
    public List<Expression<Func<Return, object>>> Includes { get; private set; } = [];
    public List<string> IncludeStrings { get; private set; } = [];
    public Expression<Func<Return, object>>? OrderBy { get; private set; }
    public Expression<Func<Return, object>>? OrderByDescending { get; private set; }
    public int? Take { get; private set; }
    public int? Skip { get; private set; }
    public bool IsPagingEnabled { get; private set; }

    /// <summary>
    /// Creates a specification that loads all navigation properties (for listing/display).
    /// </summary>
    public ReturnSpecification()
    {
        AddInclude(r => r.SparePart);
        AddInclude(r => r.User);
        AddInclude(r => r.Invoice);
        AddInclude(r => r.ReplacementPart);
    }

    /// <summary>
    /// Creates a specification that filters returns by invoice ID.
    /// </summary>
    public ReturnSpecification(int invoiceId)
    {
        AddInclude(r => r.SparePart);
        AddInclude(r => r.User);
        AddInclude(r => r.Invoice);
        AddInclude(r => r.ReplacementPart);
        Criteria = r => r.InvoiceId == invoiceId;
    }

    /// <summary>
    /// Creates a specification that filters returns by invoice ID and spare part ID.
    /// </summary>
    public ReturnSpecification(int invoiceId, int sparePartId)
    {
        AddInclude(r => r.SparePart);
        AddInclude(r => r.User);
        AddInclude(r => r.Invoice);
        Criteria = r => r.InvoiceId == invoiceId && r.SparePartId == sparePartId;
    }

    /// <summary>
    /// Creates a specification that filters returns by a set of invoice IDs.
    /// يستخدم لجلب المرتجعات لفواتير محددة فقط بدلاً من تحميل الكل.
    /// </summary>
    public ReturnSpecification(HashSet<int> invoiceIds)
    {
        AddInclude(r => r.SparePart);
        AddInclude(r => r.User);
        AddInclude(r => r.Invoice);
        AddInclude(r => r.ReplacementPart);
        Criteria = r => r.InvoiceId.HasValue && invoiceIds.Contains(r.InvoiceId.Value);
    }

    /// <summary>
    /// Creates a paged specification with date range filters (database-side filtering).
    /// </summary>
    public ReturnSpecification(int pageNumber, int pageSize, DateTime? fromDate = null, DateTime? toDate = null)
    {
        AddInclude(r => r.SparePart);
        AddInclude(r => r.User);
        AddInclude(r => r.Invoice);
        AddInclude(r => r.ReplacementPart);
        ApplyOrderByDescending(r => r.ReturnDate);

        Expression<Func<Return, bool>>? criteria = null;

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            Expression<Func<Return, bool>> fromCriteria = r => r.ReturnDate >= from;
            criteria = criteria is null ? fromCriteria : CombineAnd(criteria, fromCriteria);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date.AddDays(1).AddTicks(-1);
            Expression<Func<Return, bool>> toCriteria = r => r.ReturnDate <= to;
            criteria = criteria is null ? toCriteria : CombineAnd(criteria, toCriteria);
        }

        if (criteria is not null)
            Criteria = criteria;

        if (pageNumber > 0 && pageSize > 0)
        {
            Skip = (pageNumber - 1) * pageSize;
            Take = pageSize;
            IsPagingEnabled = true;
        }
    }

    private void AddInclude(Expression<Func<Return, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    private void ApplyOrderByDescending(Expression<Func<Return, object>> orderByDescExpression)
    {
        OrderByDescending = orderByDescExpression;
    }

    private static Expression<Func<Return, bool>> CombineAnd(
        Expression<Func<Return, bool>> first,
        Expression<Func<Return, bool>> second)
    {
        var parameter = Expression.Parameter(typeof(Return), "r");

        var leftVisitor = new ReplaceExpressionVisitor(first.Parameters[0], parameter);
        var left = leftVisitor.Visit(first.Body);

        var rightVisitor = new ReplaceExpressionVisitor(second.Parameters[0], parameter);
        var right = rightVisitor.Visit(second.Body);

        return Expression.Lambda<Func<Return, bool>>(
            Expression.AndAlso(left!, right!), parameter);
    }

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
