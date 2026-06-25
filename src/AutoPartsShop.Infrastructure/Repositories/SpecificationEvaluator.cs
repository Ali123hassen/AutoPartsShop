using AutoPartsShop.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AutoPartsShop.Infrastructure.Repositories;

public static class SpecificationEvaluator<T> where T : class
{
    public static IQueryable<T> GetQuery(IQueryable<T> inputQuery, ISpecification<T> spec)
    {
        var query = inputQuery;

        if (spec.Criteria != null)
            query = query.Where(spec.Criteria);

        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

        // Apply string-based includes for nested navigation properties (e.g. "Items.SparePart")
        query = spec.IncludeStrings.Aggregate(query, (current, includeString) => current.Include(includeString));

        if (spec.OrderBy != null)
            query = query.OrderBy(spec.OrderBy);
        else if (spec.OrderByDescending != null)
            query = query.OrderByDescending(spec.OrderByDescending);

        if (spec.IsPagingEnabled)
        {
            query = query.Skip(spec.Skip!.Value).Take(spec.Take!.Value);
        }

        return query;
    }
}