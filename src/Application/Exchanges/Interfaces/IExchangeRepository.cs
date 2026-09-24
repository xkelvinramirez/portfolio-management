using Application.Common.Repository;
using Domain.Entities;

namespace Application.Exchanges.Interfaces;

public interface IExchangeRepository : IRepository<Exchange>
{
    Task<Exchange?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<Exchange?> GetByNameAsync(string name, CancellationToken cancellationToken);
}
