using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Interfaces;
using System.Linq.Expressions;

namespace AutoPartsShop.Application.Specifications;

/// <summary>
/// Specification that returns all Setting entities.
/// Used when we need tracked entities for bulk updates.
/// </summary>
public class AllSettingsSpecification : ISpecification<Setting>
{
    public Expression<Func<Setting, bool>>? Criteria => null;
    public List<Expression<Func<Setting, object>>> Includes { get; } = [];
    public List<string> IncludeStrings { get; } = [];
    public Expression<Func<Setting, object>>? OrderBy => s => s.Id;
    public Expression<Func<Setting, object>>? OrderByDescending => null;
    public int? Take => null;
    public int? Skip => null;
    public bool IsPagingEnabled => false;
}
