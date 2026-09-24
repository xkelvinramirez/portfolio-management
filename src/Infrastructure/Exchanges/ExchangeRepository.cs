
using Application.Exchanges.Interfaces;
using Domain.Entities;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Exchanges;

internal sealed class ExchangeRepository(AppDbContext dbContext) : Repository<Exchange>(dbContext), IExchangeRepository
{
    public Task<Exchange?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => dbContext.Set<Exchange>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Exchange?> GetByNameAsync(string name, CancellationToken cancellationToken)
        => dbContext.Set<Exchange>().FirstOrDefaultAsync(e => e.Name == name, cancellationToken);
}
