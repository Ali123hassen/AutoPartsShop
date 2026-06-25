namespace AutoPartsShop.Core.Interfaces;

/// <summary>
/// Evaluates a specification against a queryable source,
/// applying criteria, includes, ordering, and paging.
/// </summary>
/// <typeparam name="T">The entity type to evaluate.</typeparam>
public interface ISpecificationEvaluator<T> where T : Entities.BaseEntity
{
    /// <summary>
    /// Applies the given specification to the queryable source and returns the resulting query.
    /// </summary>
    /// <param name="inputQuery">The base queryable to apply the specification to.</param>
    /// <param name="specification">The specification containing criteria, includes, ordering, and paging.</param>
    /// <returns>A queryable with the specification applied.</returns>
    IQueryable<T> Evaluate(IQueryable<T> inputQuery, ISpecification<T> specification);
}
