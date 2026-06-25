using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Interfaces;
using AutoPartsShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoPartsShop.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync()
    {
        return await _dbSet.AsNoTracking().ToListAsync();
    }

    public async Task<IReadOnlyList<T>> FindAsync(ISpecification<T> specification)
    {
        var query = SpecificationEvaluator<T>.GetQuery(_dbSet, specification);
        return await query.AsNoTracking().ToListAsync();
    }

    public async Task<IReadOnlyList<T>> FindTrackedAsync(ISpecification<T> specification)
    {
        var query = SpecificationEvaluator<T>.GetQuery(_dbSet, specification);
        return await query.ToListAsync(); // No AsNoTracking - entities are tracked
    }

    public async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public Task UpdateAsync(T entity)
    {
        _context.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity)
    {
        // Soft delete: if the entity has an IsActive property, set it to false
        var isActiveProperty = typeof(T).GetProperty("IsActive");
        if (isActiveProperty != null && isActiveProperty.PropertyType == typeof(bool))
        {
            isActiveProperty.SetValue(entity, false);
            _context.Entry(entity).State = EntityState.Modified;
        }
        else
        {
            // Hard delete for entities without IsActive
            _dbSet.Remove(entity);
        }

        return Task.CompletedTask;
    }

    public async Task<int> CountAsync()
    {
        return await _dbSet.CountAsync();
    }

    public async Task<IReadOnlyList<T>> GetPagedAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        return await _dbSet
            .AsNoTracking()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}
