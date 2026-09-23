using Application.Common.Repository;
using Contracts.Common;
using Domain.Common;
using Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Common.Repository;

public abstract class Repository<TEntity>(DbContext dbContext) : IRepository<TEntity> where TEntity : Entity
{
    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken)
        => await dbContext.Set<TEntity>().AddAsync(entity, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken)
        => await dbContext.Set<TEntity>().AddRangeAsync(entities, cancellationToken);

    public Task<TEntity?> GetAsync(uint id, CancellationToken cancellationToken)
        => dbContext.Set<TEntity>().FindAsync([id, cancellationToken], cancellationToken).AsTask();

    public Task<PaginatorResponse<TEntity>> GetAllAsync(int page, int limit, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken)
        => dbContext.Set<TEntity>().AsNoTracking().Where(predicate).PaginateAsync(page, limit, cancellationToken);

    public void Add(TEntity entity)
        => dbContext.Entry(entity).State = EntityState.Added;

    public void Update(TEntity entity)
        => dbContext.Entry(entity).State = EntityState.Modified;

    public void RemoveRange(List<TEntity> entities)
        => dbContext.Set<TEntity>().RemoveRange(entities);

    public async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken)
    {
        dbContext.Set<TEntity>().UpdateRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

}


