using System.Linq.Expressions;
using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Interfaces;

namespace AutoPartsShop.Application.Specifications;

/// <summary>
/// Specification for querying User entities with their Role navigation property included.
/// </summary>
public class UserSpecification : ISpecification<User>
{
    public Expression<Func<User, bool>>? Criteria { get; private set; }
    public List<Expression<Func<User, object>>> Includes { get; private set; } = [];
    public List<string> IncludeStrings { get; private set; } = [];
    public Expression<Func<User, object>>? OrderBy { get; private set; }
    public Expression<Func<User, object>>? OrderByDescending { get; private set; }
    public int? Take { get; private set; }
    public int? Skip { get; private set; }
    public bool IsPagingEnabled { get; private set; }

    public UserSpecification()
    {
        AddInclude(u => u.Role);
        ApplyOrderByDescending(u => u.Id);
    }

    public UserSpecification(int userId) : this()
    {
        Criteria = u => u.Id == userId;
    }

    private void AddInclude(Expression<Func<User, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    private void ApplyOrderBy(Expression<Func<User, object>> orderByExpression)
    {
        OrderBy = orderByExpression;
    }

    private void ApplyOrderByDescending(Expression<Func<User, object>> orderByDescExpression)
    {
        OrderByDescending = orderByDescExpression;
    }
}
