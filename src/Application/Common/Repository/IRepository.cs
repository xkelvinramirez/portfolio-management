using Contracts.Common;
using Domain.Common;
using System.Linq.Expressions;

namespace Application.Common.Repository;

public interface IRepository<TEntity> where TEntity : Entity
{
    Task AddAsync(TEntity entity, CancellationToken cancellationToken);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken);
    Task<TEntity?> GetAsync(uint id, CancellationToken cancellationToken);
    Task<PaginatorResponse<TEntity>> GetAllAsync(int page, int limit, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken);
    void Add(TEntity entity);
    void Update(TEntity entity);
    void RemoveRange(List<TEntity> entities);
    Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken);
}