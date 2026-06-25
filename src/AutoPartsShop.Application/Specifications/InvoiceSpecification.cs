using System.Linq.Expressions;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Enums;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Specifications;

/// <summary>
/// Specification for filtering Invoice entities by date range and status.
/// </summary>
public class InvoiceSpecification : ISpecification<Invoice>
{
    public Expression<Func<Invoice, bool>>? Criteria { get; private set; }
    public List<Expression<Func<Invoice, object>>> Includes { get; private set; } = [];
    public List<string> IncludeStrings { get; private set; } = [];
    public Expression<Func<Invoice, object>>? OrderBy { get; private set; }
    public Expression<Func<Invoice, object>>? OrderByDescending { get; private set; }
    public int? Take { get; private set; }
    public int? Skip { get; private set; }
    public bool IsPagingEnabled { get; private set; }

    /// <summary>
    /// Creates a specification with optional date range and status filters.
    /// </summary>
    /// <param name="fromDate">Start date filter (inclusive).</param>
    /// <param name="toDate">End date filter (inclusive).</param>
    /// <param name="status">Invoice status filter, or null for all statuses.</param>
    public InvoiceSpecification(DateTime? fromDate = null, DateTime? toDate = null, InvoiceStatus? status = null)
    {
        AddInclude(i => i.User);
        AddInclude(i => i.Items);
        AddIncludeString("Items.SparePart");
        ApplyOrderByDescending(i => i.InvoiceDate);

        Expression<Func<Invoice, bool>>? criteria = null;

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            Expression<Func<Invoice, bool>> fromCriteria = i => i.InvoiceDate >= from;
            criteria = criteria is null ? fromCriteria : CombineAnd(criteria, fromCriteria);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date.AddDays(1).AddTicks(-1);
            Expression<Func<Invoice, bool>> toCriteria = i => i.InvoiceDate <= to;
            criteria = criteria is null ? toCriteria : CombineAnd(criteria, toCriteria);
        }

        if (status.HasValue)
        {
            var statusValue = status.Value;
            Expression<Func<Invoice, bool>> statusCriteria = i => i.Status == statusValue;
            criteria = criteria is null ? statusCriteria : CombineAnd(criteria, statusCriteria);
        }

        if (criteria is not null)
            Criteria = criteria;
    }

    /// <summary>
    /// Creates a specification that filters by invoice ID (for fetching a single invoice with includes).
    /// </summary>
    /// <param name="invoiceId">The invoice ID to filter by.</param>
    public InvoiceSpecification(int invoiceId)
    {
        AddInclude(i => i.User);
        AddInclude(i => i.Items);
        AddIncludeString("Items.SparePart");
        Criteria = i => i.Id == invoiceId;
    }

    /// <summary>
    /// Creates a specification that filters by invoice number (for searching by number with includes).
    /// </summary>
    /// <param name="invoiceNumber">The invoice number to search for.</param>
    public InvoiceSpecification(string invoiceNumber)
    {
        AddInclude(i => i.User);
        AddInclude(i => i.Items);
        AddIncludeString("Items.SparePart");
        Criteria = i => i.InvoiceNumber == invoiceNumber;
    }

    /// <summary>
    /// Creates a paged specification with date range and status filters.
    /// </summary>
    public InvoiceSpecification(int pageNumber, int pageSize, DateTime? fromDate = null, DateTime? toDate = null, InvoiceStatus? status = null)
        : this(fromDate, toDate, status)
    {
        if (pageNumber > 0 && pageSize > 0)
        {
            Skip = (pageNumber - 1) * pageSize;
            Take = pageSize;
            IsPagingEnabled = true;
        }
    }

    private static Expression<Func<Invoice, bool>> CombineAnd(
        Expression<Func<Invoice, bool>> first,
        Expression<Func<Invoice, bool>> second)
    {
        var parameter = Expression.Parameter(typeof(Invoice), "i");

        var leftVisitor = new ReplaceExpressionVisitor(first.Parameters[0], parameter);
        var left = leftVisitor.Visit(first.Body);

        var rightVisitor = new ReplaceExpressionVisitor(second.Parameters[0], parameter);
        var right = rightVisitor.Visit(second.Body);

        return Expression.Lambda<Func<Invoice, bool>>(
            Expression.AndAlso(left!, right!), parameter);
    }

    private void AddInclude(Expression<Func<Invoice, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    private void AddIncludeString(string includeString)
    {
        IncludeStrings.Add(includeString);
    }

    private void ApplyOrderByDescending(Expression<Func<Invoice, object>> orderByDescExpression)
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
